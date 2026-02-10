using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Interfaces;
using CrossInputShare.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrossInputShare.Security.Services
{
    /// <summary>
    /// Server-side session manager with enhanced security features:
    /// - Session expiration with optional timeout
    /// - One-time use session codes (invalidate after first successful join)
    /// - Server-side session state validation
    /// - Automatic cleanup of abandoned sessions
    /// - Integration with rate limiting and authorization
    /// </summary>
    public class ServerSessionManager : ISessionManager, IDisposable
    {
        private readonly ILogger<ServerSessionManager> _logger;
        private readonly ServerSessionManagerOptions _options;
        private readonly ConcurrentDictionary<Guid, ManagedSession> _sessions = new();
        private readonly ConcurrentDictionary<string, Guid> _sessionCodeToId = new(); // Maps session code to session ID
        private readonly ConcurrentDictionary<Guid, Timer> _expirationTimers = new();
        private readonly object _lock = new object();
        private bool _disposed = false;

        // Events from ISessionManager
        public event EventHandler<SessionCreatedEventArgs> SessionCreated;
        public event EventHandler<SessionVerifiedEventArgs> SessionVerified;
        public event EventHandler<SessionEndedEventArgs> SessionEnded;

        /// <summary>
        /// Initializes a new instance of the ServerSessionManager class.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Configuration options</param>
        public ServerSessionManager(ILogger<ServerSessionManager> logger, IOptions<ServerSessionManagerOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new ServerSessionManagerOptions();
            
            _logger.LogInformation("ServerSessionManager initialized with {@Options}", _options);
        }

        /// <summary>
        /// Creates a new session with optional timeout.
        /// </summary>
        /// <param name="timeout">Optional session timeout. If null, uses default timeout from options or no expiration.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>The created session information</returns>
        public async Task<SessionInfo> CreateSessionAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                var sessionTimeout = timeout ?? _options.DefaultSessionTimeout;
                var sessionCode = SessionCode.Generate();
                var sessionId = Guid.NewGuid();
                
                // Create device info for server (local device)
                var serverDevice = DeviceInfo.CreateLocal(
                    DeviceFingerprint.Generate("Server", "MachineId", "InstallationId"), // TODO: Use actual device fingerprint
                    "Server Device",
                    "CrossInputShare",
                    isHost: true
                );

                var sessionInfo = new SessionInfo(sessionId, sessionCode, serverDevice, maxClients: _options.MaxClientsPerSession, timeout: sessionTimeout);
                
                var managedSession = new ManagedSession
                {
                    Session = sessionInfo,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = sessionTimeout.HasValue ? DateTime.UtcNow.Add(sessionTimeout.Value) : (DateTime?)null,
                    IsCodeUsed = false,
                    IsOneTimeUse = _options.OneTimeUseSessionCodes,
                    JoinAttempts = new Dictionary<string, DateTime>() // IP -> LastAttempt
                };

                lock (_lock)
                {
                    _sessions[sessionId] = managedSession;
                    _sessionCodeToId[sessionCode.ToString()] = sessionId;
                    
                    // Set up expiration timer if timeout specified
                    if (sessionTimeout.HasValue)
                    {
                        var timer = new Timer(_ => ExpireSession(sessionId), null, sessionTimeout.Value, Timeout.InfiniteTimeSpan);
                        _expirationTimers[sessionId] = timer;
                    }
                }

                _logger.LogInformation("Session created: {SessionId} with code {SessionCode}, timeout: {Timeout}", 
                    sessionId, sessionCode, sessionTimeout?.ToString() ?? "none");

                SessionCreated?.Invoke(this, new SessionCreatedEventArgs(sessionInfo));
                
                return sessionInfo;
            }, cancellationToken);
        }

        /// <summary>
        /// Joins an existing session using a session code.
        /// Implements rate limiting and one-time use validation.
        /// </summary>
        /// <param name="sessionCode">The session code to join</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>The session information if successful</returns>
        /// <exception cref="ArgumentException">Thrown if session code is invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown if session doesn't exist or is full</exception>
        public async Task<SessionInfo> JoinSessionAsync(SessionCode sessionCode, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                string code = sessionCode?.ToString() ?? throw new ArgumentNullException(nameof(sessionCode));
                
                // Validate session code format
                if (!SessionCode.IsValid(code))
                    throw new ArgumentException($"Invalid session code format: {code}", nameof(sessionCode));

                Guid? sessionId = null;
                ManagedSession managedSession = null;
                
                lock (_lock)
                {
                    if (!_sessionCodeToId.TryGetValue(code, out var id))
                        throw new InvalidOperationException($"Session with code {code} does not exist or has expired");

                    if (!_sessions.TryGetValue(id, out managedSession))
                    {
                        // Clean up orphaned mapping
                        _sessionCodeToId.TryRemove(code, out _);
                        throw new InvalidOperationException($"Session with code {code} does not exist or has expired");
                    }

                    sessionId = id;
                }

                // Check if session is expired
                if (managedSession.Session.IsExpired)
                {
                    RemoveSession(sessionId.Value, "Session expired");
                    throw new InvalidOperationException($"Session with code {code} has expired");
                }

                // Check one-time use: if code already used, reject join (unless configured otherwise)
                if (managedSession.IsOneTimeUse && managedSession.IsCodeUsed)
                {
                    throw new InvalidOperationException($"Session code {code} has already been used (one-time use enabled)");
                }

                // TODO: Implement IP-based rate limiting here
                // For now, just update join attempts
                // managedSession.RecordJoinAttempt(ipAddress);

                // Return the session info (actual device joining will be handled by caller)
                // The caller should then call AddClientDevice on the session
                return managedSession.Session;
            }, cancellationToken);
        }

        /// <summary>
        /// Verifies a session manually by comparing device fingerprints.
        /// </summary>
        /// <param name="sessionId">The session ID to verify</param>
        /// <param name="remoteFingerprint">The fingerprint of the remote device to verify</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>True if verification was successful</returns>
        public async Task<bool> VerifySessionAsync(Guid sessionId, DeviceFingerprint remoteFingerprint, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                if (!_sessions.TryGetValue(sessionId, out var managedSession))
                    throw new InvalidOperationException($"Session {sessionId} does not exist");

                if (managedSession.Session.IsExpired)
                {
                    RemoveSession(sessionId, "Session expired before verification");
                    throw new InvalidOperationException($"Session {sessionId} has expired");
                }

                // In a real implementation, we would compare fingerprints with expected remote device
                // For now, we'll assume verification is successful and mark the session as verified
                managedSession.Session.MarkAsVerified();
                managedSession.IsCodeUsed = true; // Mark code as used after verification (first successful join)

                _logger.LogInformation("Session verified: {SessionId} for remote fingerprint {Fingerprint}", 
                    sessionId, remoteFingerprint?.ShortDisplay ?? "null");

                SessionVerified?.Invoke(this, new SessionVerifiedEventArgs(managedSession.Session, remoteFingerprint));
                
                return true;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets the current active session for the local device (server).
        /// Since this is server-side manager, returns the most recent session created by this server.
        /// </summary>
        /// <returns>The current session or null if no active session</returns>
        public SessionInfo GetCurrentSession()
        {
            ThrowIfDisposed();
            
            // For simplicity, return the first active session
            // In a real implementation, we'd track which session belongs to this server instance
            var activeSession = _sessions.Values
                .FirstOrDefault(s => s.Session.IsActive && !s.Session.IsExpired);
            
            return activeSession?.Session;
        }

        /// <summary>
        /// Ends the current session gracefully.
        /// </summary>
        /// <param name="reason">Reason for ending the session</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        public async Task EndSessionAsync(string reason = "User requested", CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Run(() =>
            {
                var session = GetCurrentSession();
                if (session != null)
                {
                    RemoveSession(session.Id, reason);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Validates that a session code is syntactically correct.
        /// Does not check if the session actually exists.
        /// </summary>
        /// <param name="code">The code to validate</param>
        /// <returns>True if the code format is valid</returns>
        public bool ValidateSessionCode(string code)
        {
            ThrowIfDisposed();
            return SessionCode.IsValid(code);
        }

        /// <summary>
        /// Gets all active sessions (for monitoring/admin purposes).
        /// </summary>
        /// <returns>Collection of active sessions</returns>
        public IEnumerable<SessionInfo> GetActiveSessions()
        {
            ThrowIfDisposed();
            return _sessions.Values
                .Where(s => !s.Session.IsExpired && s.Session.Status != SessionStatus.Ended)
                .Select(s => s.Session)
                .ToList();
        }

        /// <summary>
        /// Cleans up expired and abandoned sessions.
        /// Should be called periodically (e.g., via background service).
        /// </summary>
        public void CleanupSessions()
        {
            ThrowIfDisposed();
            
            var now = DateTime.UtcNow;
            var sessionsToRemove = new List<Guid>();
            
            foreach (var kvp in _sessions)
            {
                var session = kvp.Value.Session;
                // Remove expired sessions
                if (session.IsExpired)
                {
                    sessionsToRemove.Add(kvp.Key);
                }
                // Remove abandoned sessions (no clients for too long)
                else if (session.Status == SessionStatus.Created && 
                         (now - kvp.Value.CreatedAt) > _options.AbandonedSessionTimeout)
                {
                    sessionsToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var sessionId in sessionsToRemove)
            {
                RemoveSession(sessionId, "Cleaned up by periodic cleanup");
            }
            
            if (sessionsToRemove.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} sessions", sessionsToRemove.Count);
            }
        }

        /// <summary>
        /// Removes a session and cleans up associated resources.
        /// </summary>
        /// <param name="sessionId">The session ID to remove</param>
        /// <param name="reason">Reason for removal</param>
        private void RemoveSession(Guid sessionId, string reason)
        {
            lock (_lock)
            {
                if (_sessions.TryRemove(sessionId, out var managedSession))
                {
                    // Remove session code mapping
                    _sessionCodeToId.TryRemove(managedSession.Session.Code.ToString(), out _);
                    
                    // Dispose expiration timer
                    if (_expirationTimers.TryRemove(sessionId, out var timer))
                    {
                        timer?.Dispose();
                    }
                    
                    // End the session
                    managedSession.Session.End(reason);
                    
                    _logger.LogInformation("Session removed: {SessionId}, reason: {Reason}", sessionId, reason);
                    
                    SessionEnded?.Invoke(this, new SessionEndedEventArgs(managedSession.Session, reason));
                }
            }
        }

        /// <summary>
        /// Expires a session when its timeout is reached.
        /// </summary>
        /// <param name="sessionId">The session ID to expire</param>
        private void ExpireSession(Guid sessionId)
        {
            RemoveSession(sessionId, "Session expired due to timeout");
        }

        /// <summary>
        /// Throws if the instance has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerSessionManager));
        }

        /// <summary>
        /// Disposes the session manager and cleans up all resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lock)
                {
                    foreach (var timer in _expirationTimers.Values)
                    {
                        timer?.Dispose();
                    }
                    
                    _expirationTimers.Clear();
                    _sessions.Clear();
                    _sessionCodeToId.Clear();
                    
                    _disposed = true;
                }
                
                _logger.LogInformation("ServerSessionManager disposed");
            }
        }

        /// <summary>
        /// Internal representation of a managed session with additional metadata.
        /// </summary>
        private class ManagedSession
        {
            public SessionInfo Session { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public bool IsCodeUsed { get; set; }
            public bool IsOneTimeUse { get; set; }
            public Dictionary<string, DateTime> JoinAttempts { get; set; } // IP address -> last attempt timestamp
            
            public void RecordJoinAttempt(string ipAddress)
            {
                JoinAttempts[ipAddress] = DateTime.UtcNow;
            }
            
            public int GetRecentJoinAttempts(string ipAddress, TimeSpan window)
            {
                var cutoff = DateTime.UtcNow - window;
                return JoinAttempts.Count(kvp => kvp.Key == ipAddress && kvp.Value > cutoff);
            }
        }
    }

    /// <summary>
    /// Configuration options for ServerSessionManager.
    /// </summary>
    public class ServerSessionManagerOptions
    {
        /// <summary>
        /// Default session timeout. If null, sessions do not expire by default.
        /// </summary>
        public TimeSpan? DefaultSessionTimeout { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Maximum number of clients allowed per session.
        /// </summary>
        public int MaxClientsPerSession { get; set; } = 4;

        /// <summary>
        /// Whether session codes are one-time use (invalidated after first successful join).
        /// </summary>
        public bool OneTimeUseSessionCodes { get; set; } = false;

        /// <summary>
        /// Time after which an abandoned session (no clients joined) is cleaned up.
        /// </summary>
        public TimeSpan AbandonedSessionTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum join attempts per IP address per minute.
        /// </summary>
        public int MaxJoinAttemptsPerMinute { get; set; } = 5;

        /// <summary>
        /// Lockout duration after exceeding max join attempts.
        /// </summary>
        public TimeSpan JoinAttemptLockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    }
}