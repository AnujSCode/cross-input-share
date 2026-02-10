using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrossInputShare.Security.Services
{
    /// <summary>
    /// Rate limiting service with IP-based throttling, exponential backoff, and IP reputation tracking.
    /// Supports multiple rate limit configurations for different operation types.
    /// </summary>
    public class RateLimiterService : IRateLimiterService, IDisposable
    {
        private readonly ILogger<RateLimiterService> _logger;
        private readonly RateLimiterOptions _options;
        private readonly ConcurrentDictionary<string, ClientRateLimitInfo> _clientInfo = new();
        private readonly ConcurrentDictionary<string, IPReputation> _ipReputation = new();
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new object();
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the RateLimiterService class.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Configuration options</param>
        public RateLimiterService(ILogger<RateLimiterService> logger, IOptions<RateLimiterOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new RateLimiterOptions();
            
            // Start periodic cleanup timer
            _cleanupTimer = new Timer(_ => CleanupOldEntries(), null, 
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes), 
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));
            
            _logger.LogInformation("RateLimiterService initialized with {@Options}", _options);
        }

        /// <summary>
        /// Checks if an operation is allowed for the specified client identifier (e.g., IP address).
        /// Implements sliding window rate limiting with configurable limits.
        /// </summary>
        /// <param name="clientId">Client identifier (usually IP address)</param>
        /// <param name="operation">Operation type (e.g., "JoinAttempt", "Authentication")</param>
        /// <param name="cost">Cost of the operation (defaults to 1)</param>
        /// <returns>Rate limit result indicating if allowed and remaining attempts</returns>
        public RateLimitResult IsAllowed(string clientId, string operation, int cost = 1)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentException("Client identifier cannot be null or empty", nameof(clientId));
            
            if (string.IsNullOrEmpty(operation))
                throw new ArgumentException("Operation cannot be null or empty", nameof(operation));
            
            // Check IP reputation first (global ban)
            var reputation = GetIPReputation(clientId);
            if (reputation.IsBlocked)
            {
                _logger.LogWarning("Rate limit blocked: Client {ClientId} is blocked due to reputation until {BlockedUntil}", 
                    clientId, reputation.BlockedUntil);
                return RateLimitResult.Blocked(reputation.BlockedUntil.Value - DateTime.UtcNow, "IP blocked due to malicious activity");
            }

            // Get or create client rate limit info
            var client = _clientInfo.GetOrAdd(clientId, id => new ClientRateLimitInfo(id));
            
            // Get rate limit configuration for this operation
            var limitConfig = GetRateLimitConfiguration(operation);
            
            lock (client.Lock)
            {
                // Clean old attempts outside the sliding window
                var windowStart = DateTime.UtcNow - limitConfig.Window;
                client.Attempts[operation] = client.Attempts.TryGetValue(operation, out var attempts)
                    ? attempts.Where(a => a.Timestamp > windowStart).ToList()
                    : new List<AttemptRecord>();
                
                // Calculate total cost in current window
                int totalCost = client.Attempts[operation].Sum(a => a.Cost);
                
                // Check if adding this operation would exceed limit
                if (totalCost + cost > limitConfig.MaxAttempts)
                {
                    // Determine oldest attempt to calculate retry after
                    var oldestAttempt = client.Attempts[operation].MinBy(a => a.Timestamp);
                    var retryAfter = oldestAttempt != null 
                        ? (oldestAttempt.Timestamp + limitConfig.Window) - DateTime.UtcNow
                        : limitConfig.Window;
                    
                    // Record failed attempt for reputation tracking
                    RecordFailedAttempt(clientId, operation);
                    
                    _logger.LogWarning("Rate limit exceeded: Client {ClientId}, operation {Operation}, attempts {TotalCost}/{MaxAttempts}", 
                        clientId, operation, totalCost, limitConfig.MaxAttempts);
                    
                    return RateLimitResult.LimitExceeded(retryAfter, totalCost, limitConfig.MaxAttempts);
                }
                
                // Operation allowed - record attempt
                client.Attempts[operation].Add(new AttemptRecord
                {
                    Timestamp = DateTime.UtcNow,
                    Cost = cost,
                    Successful = false // Will be updated if operation succeeds
                });
                
                // Trim attempts to keep only recent ones (prevent memory leak)
                if (client.Attempts[operation].Count > limitConfig.MaxAttempts * 2)
                {
                    client.Attempts[operation] = client.Attempts[operation]
                        .OrderByDescending(a => a.Timestamp)
                        .Take(limitConfig.MaxAttempts)
                        .ToList();
                }
                
                int remaining = limitConfig.MaxAttempts - (totalCost + cost);
                return RateLimitResult.Allowed(remaining, limitConfig.MaxAttempts);
            }
        }

        /// <summary>
        /// Records a successful operation, which may improve client reputation.
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <param name="operation">Operation type</param>
        public void RecordSuccess(string clientId, string operation)
        {
            ThrowIfDisposed();
            
            if (_clientInfo.TryGetValue(clientId, out var client))
            {
                lock (client.Lock)
                {
                    if (client.Attempts.TryGetValue(operation, out var attempts))
                    {
                        // Mark the most recent attempt as successful
                        var recent = attempts.LastOrDefault();
                        if (recent != null)
                        {
                            recent.Successful = true;
                        }
                    }
                }
                
                // Improve IP reputation on successful operations
                ImproveIPReputation(clientId);
            }
        }

        /// <summary>
        /// Records a failed operation, which may decrease client reputation.
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <param name="operation">Operation type</param>
        public void RecordFailedAttempt(string clientId, string operation)
        {
            ThrowIfDisposed();
            
            // Decrease IP reputation
            var reputation = _ipReputation.GetOrAdd(clientId, id => new IPReputation(id));
            lock (reputation.Lock)
            {
                reputation.FailedAttempts++;
                reputation.LastFailedAttempt = DateTime.UtcNow;
                
                // Check if we should block this IP
                if (reputation.FailedAttempts >= _options.MaxFailedAttemptsBeforeBlock)
                {
                    // Exponential backoff: block duration increases with each subsequent block
                    int blockMultiplier = (int)Math.Pow(2, Math.Min(reputation.BlockCount, 10));
                    TimeSpan blockDuration = TimeSpan.FromMinutes(_options.InitialBlockDurationMinutes * blockMultiplier);
                    
                    reputation.IsBlocked = true;
                    reputation.BlockedUntil = DateTime.UtcNow + blockDuration;
                    reputation.BlockCount++;
                    
                    _logger.LogWarning("IP blocked: {ClientId} blocked for {BlockDuration} due to {FailedAttempts} failed attempts", 
                        clientId, blockDuration, reputation.FailedAttempts);
                }
            }
        }

        /// <summary>
        /// Gets the current IP reputation for a client.
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <returns>IP reputation information</returns>
        public IPReputationInfo GetIPReputationInfo(string clientId)
        {
            ThrowIfDisposed();
            
            var reputation = GetIPReputation(clientId);
            lock (reputation.Lock)
            {
                return new IPReputationInfo
                {
                    ClientId = reputation.ClientId,
                    FailedAttempts = reputation.FailedAttempts,
                    SuccessfulAttempts = reputation.SuccessfulAttempts,
                    IsBlocked = reputation.IsBlocked,
                    BlockedUntil = reputation.BlockedUntil,
                    BlockCount = reputation.BlockCount,
                    ReputationScore = CalculateReputationScore(reputation)
                };
            }
        }

        /// <summary>
        /// Resets rate limiting and reputation data for a client.
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        public void ResetClient(string clientId)
        {
            ThrowIfDisposed();
            
            _clientInfo.TryRemove(clientId, out _);
            _ipReputation.TryRemove(clientId, out _);
            
            _logger.LogInformation("Rate limit data reset for client {ClientId}", clientId);
        }

        /// <summary>
        /// Cleans up old rate limit entries to prevent memory exhaustion.
        /// Called periodically by cleanup timer.
        /// </summary>
        private void CleanupOldEntries()
        {
            ThrowIfDisposed();
            
            var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(_options.DataRetentionMinutes);
            int removedClients = 0;
            int removedReputations = 0;
            
            // Remove old client rate limit info
            foreach (var kvp in _clientInfo)
            {
                var client = kvp.Value;
                bool hasRecentActivity = false;
                
                lock (client.Lock)
                {
                    foreach (var operation in client.Attempts.Keys.ToList())
                    {
                        client.Attempts[operation] = client.Attempts[operation]
                            .Where(a => a.Timestamp > cutoff)
                            .ToList();
                        
                        if (client.Attempts[operation].Count > 0)
                            hasRecentActivity = true;
                        else
                            client.Attempts.Remove(operation);
                    }
                }
                
                if (!hasRecentActivity)
                {
                    _clientInfo.TryRemove(kvp.Key, out _);
                    removedClients++;
                }
            }
            
            // Remove old IP reputation entries where block has expired
            foreach (var kvp in _ipReputation)
            {
                var reputation = kvp.Value;
                bool shouldRemove = false;
                
                lock (reputation.Lock)
                {
                    if (reputation.IsBlocked && reputation.BlockedUntil.HasValue && reputation.BlockedUntil.Value < DateTime.UtcNow)
                    {
                        // Block has expired, reset reputation
                        reputation.IsBlocked = false;
                        reputation.BlockedUntil = null;
                        reputation.FailedAttempts = 0;
                        reputation.BlockCount = 0;
                    }
                    
                    // Remove entries with no activity for a long time
                    var lastActivity = new[] { reputation.LastFailedAttempt, reputation.LastSuccessfulAttempt }
                        .Where(d => d.HasValue)
                        .Select(d => d.Value)
                        .DefaultIfEmpty(reputation.CreatedAt)
                        .Max();
                    
                    if (DateTime.UtcNow - lastActivity > TimeSpan.FromMinutes(_options.DataRetentionMinutes))
                    {
                        shouldRemove = true;
                    }
                }
                
                if (shouldRemove)
                {
                    _ipReputation.TryRemove(kvp.Key, out _);
                    removedReputations++;
                }
            }
            
            if (removedClients > 0 || removedReputations > 0)
            {
                _logger.LogDebug("Rate limit cleanup removed {ClientCount} clients and {ReputationCount} reputation entries", 
                    removedClients, removedReputations);
            }
        }

        /// <summary>
        /// Gets the rate limit configuration for an operation type.
        /// </summary>
        private RateLimitConfig GetRateLimitConfiguration(string operation)
        {
            // Default configuration
            var defaultConfig = new RateLimitConfig
            {
                Window = TimeSpan.FromMinutes(1),
                MaxAttempts = 5
            };
            
            // Operation-specific configurations
            return operation.ToUpperInvariant() switch
            {
                "JOINATTEMPT" => new RateLimitConfig
                {
                    Window = TimeSpan.FromMinutes(1),
                    MaxAttempts = _options.MaxJoinAttemptsPerMinute
                },
                "AUTHENTICATION" => new RateLimitConfig
                {
                    Window = TimeSpan.FromMinutes(5),
                    MaxAttempts = _options.MaxAuthAttemptsPer5Minutes
                },
                "API" => new RateLimitConfig
                {
                    Window = TimeSpan.FromMinutes(1),
                    MaxAttempts = _options.MaxApiRequestsPerMinute
                },
                _ => defaultConfig
            };
        }

        private IPReputation GetIPReputation(string clientId)
        {
            return _ipReputation.GetOrAdd(clientId, id => new IPReputation(id));
        }

        private void ImproveIPReputation(string clientId)
        {
            var reputation = GetIPReputation(clientId);
            lock (reputation.Lock)
            {
                reputation.SuccessfulAttempts++;
                reputation.LastSuccessfulAttempt = DateTime.UtcNow;
                
                // Gradually decay failed attempts over time
                if (reputation.FailedAttempts > 0)
                {
                    // Reduce failed attempts by 1 for every 10 successful attempts
                    if (reputation.SuccessfulAttempts % 10 == 0)
                    {
                        reputation.FailedAttempts = Math.Max(0, reputation.FailedAttempts - 1);
                    }
                }
            }
        }

        private double CalculateReputationScore(IPReputation reputation)
        {
            // Simple reputation score: higher is better
            double score = 100.0;
            
            // Deduct points for failed attempts
            score -= reputation.FailedAttempts * 5;
            
            // Add points for successful attempts
            score += Math.Min(reputation.SuccessfulAttempts * 0.1, 20);
            
            // Severe penalty for blocks
            if (reputation.IsBlocked)
                score -= 50;
            
            score -= reputation.BlockCount * 20;
            
            return Math.Max(0, Math.Min(100, score));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RateLimiterService));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cleanupTimer?.Dispose();
                _logger.LogInformation("RateLimiterService disposed");
            }
        }

        /// <summary>
        /// Rate limit configuration for an operation type.
        /// </summary>
        private class RateLimitConfig
        {
            public TimeSpan Window { get; set; }
            public int MaxAttempts { get; set; }
        }

        /// <summary>
        /// Client rate limit information.
        /// </summary>
        private class ClientRateLimitInfo
        {
            public string ClientId { get; }
            public Dictionary<string, List<AttemptRecord>> Attempts { get; }
            public object Lock { get; } = new object();

            public ClientRateLimitInfo(string clientId)
            {
                ClientId = clientId;
                Attempts = new Dictionary<string, List<AttemptRecord>>();
            }
        }

        /// <summary>
        /// Attempt record for rate limiting.
        /// </summary>
        private class AttemptRecord
        {
            public DateTime Timestamp { get; set; }
            public int Cost { get; set; }
            public bool Successful { get; set; }
        }

        /// <summary>
        /// IP reputation tracking.
        /// </summary>
        private class IPReputation
        {
            public string ClientId { get; }
            public int FailedAttempts { get; set; }
            public int SuccessfulAttempts { get; set; }
            public bool IsBlocked { get; set; }
            public DateTime? BlockedUntil { get; set; }
            public int BlockCount { get; set; }
            public DateTime CreatedAt { get; }
            public DateTime? LastFailedAttempt { get; set; }
            public DateTime? LastSuccessfulAttempt { get; set; }
            public object Lock { get; } = new object();

            public IPReputation(string clientId)
            {
                ClientId = clientId;
                CreatedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Interface for rate limiting service.
    /// </summary>
    public interface IRateLimiterService : IDisposable
    {
        /// <summary>
        /// Checks if an operation is allowed for the specified client identifier.
        /// </summary>
        RateLimitResult IsAllowed(string clientId, string operation, int cost = 1);
        
        /// <summary>
        /// Records a successful operation.
        /// </summary>
        void RecordSuccess(string clientId, string operation);
        
        /// <summary>
        /// Records a failed operation.
        /// </summary>
        void RecordFailedAttempt(string clientId, string operation);
        
        /// <summary>
        /// Gets the current IP reputation for a client.
        /// </summary>
        IPReputationInfo GetIPReputationInfo(string clientId);
        
        /// <summary>
        /// Resets rate limiting and reputation data for a client.
        /// </summary>
        void ResetClient(string clientId);
    }

    /// <summary>
    /// Result of a rate limit check.
    /// </summary>
    public class RateLimitResult
    {
        public bool IsAllowed { get; }
        public TimeSpan? RetryAfter { get; }
        public int RemainingAttempts { get; }
        public int Limit { get; }
        public string Reason { get; }

        private RateLimitResult(bool isAllowed, TimeSpan? retryAfter, int remaining, int limit, string reason)
        {
            IsAllowed = isAllowed;
            RetryAfter = retryAfter;
            RemainingAttempts = remaining;
            Limit = limit;
            Reason = reason;
        }

        public static RateLimitResult Allowed(int remaining, int limit)
        {
            return new RateLimitResult(true, null, remaining, limit, null);
        }

        public static RateLimitResult LimitExceeded(TimeSpan retryAfter, int current, int limit)
        {
            return new RateLimitResult(false, retryAfter, 0, limit, $"Rate limit exceeded. Try again in {retryAfter.TotalSeconds:F0} seconds.");
        }

        public static RateLimitResult Blocked(TimeSpan blockDuration, string reason)
        {
            return new RateLimitResult(false, blockDuration, 0, 0, reason);
        }
    }

    /// <summary>
    /// IP reputation information.
    /// </summary>
    public class IPReputationInfo
    {
        public string ClientId { get; set; }
        public int FailedAttempts { get; set; }
        public int SuccessfulAttempts { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? BlockedUntil { get; set; }
        public int BlockCount { get; set; }
        public double ReputationScore { get; set; }
    }

    /// <summary>
    /// Configuration options for RateLimiterService.
    /// </summary>
    public class RateLimiterOptions
    {
        /// <summary>
        /// Maximum join attempts per minute per IP address.
        /// </summary>
        public int MaxJoinAttemptsPerMinute { get; set; } = 5;

        /// <summary>
        /// Maximum authentication attempts per 5 minutes per IP address.
        /// </summary>
        public int MaxAuthAttemptsPer5Minutes { get; set; } = 10;

        /// <summary>
        /// Maximum API requests per minute per IP address.
        /// </summary>
        public int MaxApiRequestsPerMinute { get; set; } = 60;

        /// <summary>
        /// Maximum failed attempts before IP is blocked.
        /// </summary>
        public int MaxFailedAttemptsBeforeBlock { get; set; } = 10;

        /// <summary>
        /// Initial block duration in minutes.
        /// </summary>
        public int InitialBlockDurationMinutes { get; set; } = 15;

        /// <summary>
        /// Cleanup interval in minutes.
        /// </summary>
        public int CleanupIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Data retention period in minutes.
        /// </summary>
        public int DataRetentionMinutes { get; set; } = 60;
    }
}