using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrossInputShare.Core.Interfaces;
using CrossInputShare.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrossInputShare.Security.Services
{
    /// <summary>
    /// Authorization service for role-based access control in cross-platform input sharing.
    /// Validates permissions for operations like input sending, screen sharing, and clipboard access.
    /// Implements fail-secure defaults (deny by default) and supports fine-grained permission checks.
    /// </summary>
    public class AuthorizationService : IAuthorizationService
    {
        private readonly ILogger<AuthorizationService> _logger;
        private readonly AuthorizationOptions _options;
        private readonly ConcurrentDictionary<Guid, SessionAuthorizationContext> _sessionContexts = new();
        private readonly object _lock = new object();
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the AuthorizationService class.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Configuration options</param>
        public AuthorizationService(ILogger<AuthorizationService> logger, IOptions<AuthorizationOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new AuthorizationOptions();
            _logger.LogInformation("AuthorizationService initialized with {@Options}", _options);
        }

        /// <summary>
        /// Checks if a device is authorized to perform an operation in a session.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="deviceId">Device ID</param>
        /// <param name="operation">Operation to authorize</param>
        /// <param name="resource">Optional resource being accessed (e.g., screen sharing target)</param>
        /// <returns>Authorization result with details</returns>
        public async Task<AuthorizationResult> AuthorizeAsync(Guid sessionId, Guid deviceId, string operation, string resource = null)
        {
            ThrowIfDisposed();
            
            if (sessionId == Guid.Empty)
                throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));
            
            if (deviceId == Guid.Empty)
                throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));
            
            if (string.IsNullOrEmpty(operation))
                throw new ArgumentException("Operation cannot be null or empty", nameof(operation));

            return await Task.Run(() =>
            {
                // Get session context (or create default if not exists)
                var context = GetOrCreateSessionContext(sessionId);
                
                // Get device role in session
                var deviceRole = context.GetDeviceRole(deviceId);
                if (deviceRole == null)
                {
                    return AuthorizationResult.Denied($"Device {deviceId} is not part of session {sessionId}");
                }
                
                // Check if session is verified (required for sensitive operations)
                if (context.RequiresSessionVerification && !context.IsSessionVerified)
                {
                    if (IsSensitiveOperation(operation))
                    {
                        return AuthorizationResult.Denied($"Session {sessionId} requires manual verification before performing {operation}");
                    }
                }
                
                // Check operation-specific authorization
                var permission = GetRequiredPermission(operation, resource);
                if (permission == null)
                {
                    _logger.LogWarning("Unknown operation {Operation} for authorization - denying by default", operation);
                    return AuthorizationResult.Denied($"Operation {operation} is not recognized");
                }
                
                // Evaluate permission based on device role and session context
                bool isAuthorized = EvaluatePermission(permission, deviceRole.Value, context, resource);
                
                if (isAuthorized)
                {
                    _logger.LogDebug("Authorization granted: Device {DeviceId} ({Role}) can {Operation} in session {SessionId}", 
                        deviceId, deviceRole, operation, sessionId);
                    
                    return AuthorizationResult.Allowed();
                }
                else
                {
                    _logger.LogWarning("Authorization denied: Device {DeviceId} ({Role}) cannot {Operation} in session {SessionId}", 
                        deviceId, deviceRole, operation, sessionId);
                    
                    return AuthorizationResult.Denied($"Device role {deviceRole} is not authorized to perform {operation}");
                }
            });
        }

        /// <summary>
        /// Registers a session with the authorization service.
        /// Should be called when a session is created or when authorization context is needed.
        /// </summary>
        /// <param name="sessionInfo">Session information</param>
        public void RegisterSession(SessionInfo sessionInfo)
        {
            ThrowIfDisposed();
            
            if (sessionInfo == null)
                throw new ArgumentNullException(nameof(sessionInfo));
            
            var context = new SessionAuthorizationContext(sessionInfo);
            _sessionContexts[sessionInfo.Id] = context;
            
            _logger.LogInformation("Session registered for authorization: {SessionId} with {DeviceCount} devices", 
                sessionInfo.Id, sessionInfo.ConnectedDevices.Count);
        }

        /// <summary>
        /// Updates session verification status.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="isVerified">Whether the session is verified</param>
        public void UpdateSessionVerification(Guid sessionId, bool isVerified)
        {
            ThrowIfDisposed();
            
            if (_sessionContexts.TryGetValue(sessionId, out var context))
            {
                context.IsSessionVerified = isVerified;
                _logger.LogInformation("Session verification updated: {SessionId} = {IsVerified}", sessionId, isVerified);
            }
        }

        /// <summary>
        /// Adds a device to a session's authorization context.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="deviceInfo">Device information</param>
        public void AddDeviceToSession(Guid sessionId, DeviceInfo deviceInfo)
        {
            ThrowIfDisposed();
            
            if (_sessionContexts.TryGetValue(sessionId, out var context))
            {
                context.AddDevice(deviceInfo);
                _logger.LogDebug("Device added to authorization context: {DeviceId} to session {SessionId}", deviceInfo.Id, sessionId);
            }
        }

        /// <summary>
        /// Removes a device from a session's authorization context.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="deviceId">Device ID</param>
        public void RemoveDeviceFromSession(Guid sessionId, Guid deviceId)
        {
            ThrowIfDisposed();
            
            if (_sessionContexts.TryGetValue(sessionId, out var context))
            {
                context.RemoveDevice(deviceId);
                _logger.LogDebug("Device removed from authorization context: {DeviceId} from session {SessionId}", deviceId, sessionId);
            }
        }

        /// <summary>
        /// Updates session features and recalculates authorization policies.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="features">Updated session features</param>
        public void UpdateSessionFeatures(Guid sessionId, SessionFeatures features)
        {
            ThrowIfDisposed();
            
            if (_sessionContexts.TryGetValue(sessionId, out var context))
            {
                context.UpdateFeatures(features);
                _logger.LogDebug("Session features updated for {SessionId}: {@Features}", sessionId, features);
            }
        }

        /// <summary>
        /// Unregisters a session from the authorization service.
        /// Should be called when a session ends.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        public void UnregisterSession(Guid sessionId)
        {
            ThrowIfDisposed();
            
            _sessionContexts.TryRemove(sessionId, out _);
            _logger.LogDebug("Session unregistered from authorization: {SessionId}", sessionId);
        }

        /// <summary>
        /// Gets the authorization context for a session.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <returns>Authorization context or null if not found</returns>
        public SessionAuthorizationContext GetSessionContext(Guid sessionId)
        {
            ThrowIfDisposed();
            
            _sessionContexts.TryGetValue(sessionId, out var context);
            return context;
        }

        /// <summary>
        /// Evaluates whether a permission is granted for a device role in a session context.
        /// </summary>
        private bool EvaluatePermission(Permission permission, DeviceRole deviceRole, SessionAuthorizationContext context, string resource)
        {
            // Default deny
            bool allowed = false;
            
            switch (permission.Name)
            {
                case PermissionNames.SendKeyboardInput:
                    // Only server can send keyboard input
                    allowed = deviceRole == DeviceRole.Server;
                    break;
                    
                case PermissionNames.SendMouseInput:
                    // Only server can send mouse input
                    allowed = deviceRole == DeviceRole.Server;
                    break;
                    
                case PermissionNames.SendClipboardData:
                    // Server can always send clipboard data
                    if (deviceRole == DeviceRole.Server)
                        allowed = true;
                    // Clients can send clipboard data only if feature is enabled
                    else if (deviceRole == DeviceRole.Client)
                        allowed = context.IsFeatureEnabled(SessionFeatures.ClientToServerClipboard);
                    break;
                    
                case PermissionNames.ReceiveClipboardData:
                    // All devices can receive clipboard data
                    allowed = true;
                    break;
                    
                case PermissionNames.ShareScreen:
                    // Screen sharing requires session verification
                    allowed = context.IsSessionVerified || !context.RequiresSessionVerification;
                    // Additionally, check if screen sharing feature is enabled
                    allowed &= context.IsFeatureEnabled(SessionFeatures.ScreenSharing);
                    break;
                    
                case PermissionNames.ReceiveScreen:
                    // All devices can receive screen if feature enabled
                    allowed = context.IsFeatureEnabled(SessionFeatures.ScreenSharing);
                    break;
                    
                case PermissionNames.ManageSession:
                    // Only server can manage session
                    allowed = deviceRole == DeviceRole.Server;
                    break;
                    
                case PermissionNames.InviteClients:
                    // Only server can invite clients
                    allowed = deviceRole == DeviceRole.Server;
                    break;
                    
                case PermissionNames.ModifySettings:
                    // Only server can modify settings
                    allowed = deviceRole == DeviceRole.Server;
                    break;
                    
                default:
                    // Unknown permission - deny by default
                    allowed = false;
                    break;
            }
            
            // Apply resource-specific checks if needed
            if (allowed && !string.IsNullOrEmpty(resource))
            {
                // Additional checks based on resource (e.g., specific device ID for screen sharing)
                allowed = EvaluateResourcePermission(permission, deviceRole, context, resource);
            }
            
            return allowed;
        }

        /// <summary>
        /// Evaluates resource-specific permission checks.
        /// </summary>
        private bool EvaluateResourcePermission(Permission permission, DeviceRole deviceRole, SessionAuthorizationContext context, string resource)
        {
            // For screen sharing to a specific device, check if target device is in session
            if (permission.Name == PermissionNames.ShareScreen && Guid.TryParse(resource, out var targetDeviceId))
            {
                return context.ContainsDevice(targetDeviceId);
            }
            
            // Default allow if resource is valid
            return true;
        }

        /// <summary>
        /// Determines if an operation requires session verification.
        /// </summary>
        private bool IsSensitiveOperation(string operation)
        {
            var sensitiveOperations = new HashSet<string>
            {
                PermissionNames.SendKeyboardInput,
                PermissionNames.SendMouseInput,
                PermissionNames.ShareScreen,
                PermissionNames.ManageSession,
                PermissionNames.ModifySettings
            };
            
            return sensitiveOperations.Contains(operation);
        }

        /// <summary>
        /// Gets the required permission for an operation.
        /// </summary>
        private Permission GetRequiredPermission(string operation, string resource)
        {
            return operation.ToUpperInvariant() switch
            {
                "SEND_KEYBOARD_INPUT" => new Permission(PermissionNames.SendKeyboardInput, "Send keyboard input to other devices"),
                "SEND_MOUSE_INPUT" => new Permission(PermissionNames.SendMouseInput, "Send mouse input to other devices"),
                "SEND_CLIPBOARD_DATA" => new Permission(PermissionNames.SendClipboardData, "Send clipboard data to other devices"),
                "RECEIVE_CLIPBOARD_DATA" => new Permission(PermissionNames.ReceiveClipboardData, "Receive clipboard data from other devices"),
                "SHARE_SCREEN" => new Permission(PermissionNames.ShareScreen, "Share screen to other devices", resource),
                "RECEIVE_SCREEN" => new Permission(PermissionNames.ReceiveScreen, "Receive screen from other devices"),
                "MANAGE_SESSION" => new Permission(PermissionNames.ManageSession, "Manage session settings and participants"),
                "INVITE_CLIENTS" => new Permission(PermissionNames.InviteClients, "Invite new clients to session"),
                "MODIFY_SETTINGS" => new Permission(PermissionNames.ModifySettings, "Modify session settings"),
                _ => null
            };
        }

        private SessionAuthorizationContext GetOrCreateSessionContext(Guid sessionId)
        {
            return _sessionContexts.GetOrAdd(sessionId, id => new SessionAuthorizationContext(null));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AuthorizationService));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _sessionContexts.Clear();
                _logger.LogInformation("AuthorizationService disposed");
            }
        }
    }

    /// <summary>
    /// Interface for authorization service.
    /// </summary>
    public interface IAuthorizationService : IDisposable
    {
        /// <summary>
        /// Checks if a device is authorized to perform an operation in a session.
        /// </summary>
        Task<AuthorizationResult> AuthorizeAsync(Guid sessionId, Guid deviceId, string operation, string resource = null);
        
        /// <summary>
        /// Registers a session with the authorization service.
        /// </summary>
        void RegisterSession(SessionInfo sessionInfo);
        
        /// <summary>
        /// Updates session verification status.
        /// </summary>
        void UpdateSessionVerification(Guid sessionId, bool isVerified);
        
        /// <summary>
        /// Adds a device to a session's authorization context.
        /// </summary>
        void AddDeviceToSession(Guid sessionId, DeviceInfo deviceInfo);
        
        /// <summary>
        /// Removes a device from a session's authorization context.
        /// </summary>
        void RemoveDeviceFromSession(Guid sessionId, Guid deviceId);
        
        /// <summary>
        /// Updates session features and recalculates authorization policies.
        /// </summary>
        void UpdateSessionFeatures(Guid sessionId, SessionFeatures features);
        
        /// <summary>
        /// Unregisters a session from the authorization service.
        /// </summary>
        void UnregisterSession(Guid sessionId);
        
        /// <summary>
        /// Gets the authorization context for a session.
        /// </summary>
        SessionAuthorizationContext GetSessionContext(Guid sessionId);
    }

    /// <summary>
    /// Authorization result.
    /// </summary>
    public class AuthorizationResult
    {
        public bool IsAuthorized { get; }
        public string Reason { get; }
        public DateTime Timestamp { get; }

        private AuthorizationResult(bool isAuthorized, string reason)
        {
            IsAuthorized = isAuthorized;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }

        public static AuthorizationResult Allowed()
        {
            return new AuthorizationResult(true, "Operation authorized");
        }

        public static AuthorizationResult Denied(string reason)
        {
            return new AuthorizationResult(false, reason ?? "Operation denied");
        }
    }

    /// <summary>
    /// Session authorization context containing authorization state for a session.
    /// </summary>
    public class SessionAuthorizationContext
    {
        private readonly SessionInfo _sessionInfo;
        private readonly Dictionary<Guid, DeviceRole> _deviceRoles = new();
        private SessionFeatures _features;
        private bool _isSessionVerified;

        public SessionAuthorizationContext(SessionInfo sessionInfo)
        {
            _sessionInfo = sessionInfo;
            _features = sessionInfo?.EnabledFeatures ?? SessionFeatures.Default;
            _isSessionVerified = sessionInfo?.IsVerified ?? false;
            
            if (sessionInfo != null)
            {
                // Initialize device roles
                foreach (var device in sessionInfo.ConnectedDevices)
                {
                    _deviceRoles[device.Id] = sessionInfo.GetDeviceRole(device.Id);
                }
            }
        }

        public bool IsSessionVerified
        {
            get => _isSessionVerified;
            set => _isSessionVerified = value;
        }

        public bool RequiresSessionVerification => true; // Could be configurable

        public DeviceRole? GetDeviceRole(Guid deviceId)
        {
            if (_deviceRoles.TryGetValue(deviceId, out var role))
                return role;
            
            // Fall back to session info if available
            if (_sessionInfo != null)
            {
                try
                {
                    return _sessionInfo.GetDeviceRole(deviceId);
                }
                catch
                {
                    return null;
                }
            }
            
            return null;
        }

        public bool ContainsDevice(Guid deviceId)
        {
            return _deviceRoles.ContainsKey(deviceId) || 
                   (_sessionInfo?.ConnectedDevices.Any(d => d.Id == deviceId) ?? false);
        }

        public bool IsFeatureEnabled(SessionFeatures feature)
        {
            return _features.HasFlag(feature);
        }

        public void AddDevice(DeviceInfo deviceInfo)
        {
            if (_sessionInfo != null)
            {
                var role = _sessionInfo.GetDeviceRole(deviceInfo.Id);
                _deviceRoles[deviceInfo.Id] = role;
            }
            else
            {
                // Default to client if we don't have session info
                _deviceRoles[deviceInfo.Id] = DeviceRole.Client;
            }
        }

        public void RemoveDevice(Guid deviceId)
        {
            _deviceRoles.Remove(deviceId);
        }

        public void UpdateFeatures(SessionFeatures features)
        {
            _features = features;
        }
    }

    /// <summary>
    /// Permission representation.
    /// </summary>
    public class Permission
    {
        public string Name { get; }
        public string Description { get; }
        public string Resource { get; }

        public Permission(string name, string description, string resource = null)
        {
            Name = name;
            Description = description;
            Resource = resource;
        }
    }

    /// <summary>
    /// Permission names constants.
    /// </summary>
    public static class PermissionNames
    {
        public const string SendKeyboardInput = "SEND_KEYBOARD_INPUT";
        public const string SendMouseInput = "SEND_MOUSE_INPUT";
        public const string SendClipboardData = "SEND_CLIPBOARD_DATA";
        public const string ReceiveClipboardData = "RECEIVE_CLIPBOARD_DATA";
        public const string ShareScreen = "SHARE_SCREEN";
        public const string ReceiveScreen = "RECEIVE_SCREEN";
        public const string ManageSession = "MANAGE_SESSION";
        public const string InviteClients = "INVITE_CLIENTS";
        public const string ModifySettings = "MODIFY_SETTINGS";
    }

    /// <summary>
    /// Configuration options for AuthorizationService.
    /// </summary>
    public class AuthorizationOptions
    {
        /// <summary>
        /// Whether to require session verification for sensitive operations.
        /// </summary>
        public bool RequireSessionVerification { get; set; } = true;

        /// <summary>
        /// Whether clients can send clipboard data to server.
        /// </summary>
        public bool AllowClientToServerClipboard { get; set; } = true;

        /// <summary>
        /// Whether clients can share screen.
        /// </summary>
        public bool AllowClientScreenSharing { get; set; } = true;

        /// <summary>
        /// Default permission policy (allow/deny) for unknown operations.
        /// </summary>
        public string DefaultPolicy { get; set; } = "Deny";
    }
}