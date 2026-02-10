using System;
using System.Collections.Generic;
using System.Linq;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Represents information about a sharing session.
    /// Contains session metadata, participants, and status.
    /// </summary>
    public class SessionInfo
    {
        /// <summary>
        /// Unique identifier for the session.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// The session code used to join the session.
        /// </summary>
        public SessionCode Code { get; }

        /// <summary>
        /// The device that created the session (server).
        /// Server provides keyboard/mouse input and manages connections.
        /// </summary>
        public DeviceInfo ServerDevice { get; }

        /// <summary>
        /// List of devices currently connected to the session.
        /// </summary>
        public IReadOnlyList<DeviceInfo> ConnectedDevices { get; private set; }

        /// <summary>
        /// Gets all client devices (excluding the server).
        /// </summary>
        public IReadOnlyList<DeviceInfo> ClientDevices
        {
            get
            {
                var clients = new List<DeviceInfo>();
                foreach (var device in ConnectedDevices)
                {
                    if (device.Id != ServerDevice.Id)
                    {
                        clients.Add(device);
                    }
                }
                return clients.AsReadOnly();
            }
        }

        /// <summary>
        /// Maximum number of clients allowed in the session.
        /// </summary>
        public int MaxClients { get; private set; } = 4; // Server + 4 clients = 5 total devices

        /// <summary>
        /// When the session was created.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// When the session will expire (if timeout is set).
        /// </summary>
        public DateTime? ExpiresAt { get; private set; }

        /// <summary>
        /// Current status of the session.
        /// </summary>
        public SessionStatus Status { get; private set; }

        /// <summary>
        /// Whether the session has been manually verified.
        /// Manual verification is required before sensitive operations.
        /// </summary>
        public bool IsVerified { get; private set; }

        /// <summary>
        /// Features that are enabled for this session.
        /// Users can toggle these features on/off.
        /// </summary>
        public SessionFeatures EnabledFeatures { get; private set; }

        /// <summary>
        /// Creates a new session with the specified parameters.
        /// </summary>
        /// <param name="id">Session ID</param>
        /// <param name="code">Session code</param>
        /// <param name="serverDevice">Server device information (provides input)</param>
        /// <param name="maxClients">Maximum number of client devices allowed</param>
        /// <param name="timeout">Optional session timeout</param>
        public SessionInfo(Guid id, SessionCode code, DeviceInfo serverDevice, int maxClients = 4, TimeSpan? timeout = null)
        {
            Id = id;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            ServerDevice = serverDevice ?? throw new ArgumentNullException(nameof(serverDevice));
            MaxClients = maxClients;
            CreatedAt = DateTime.UtcNow;
            
            if (timeout.HasValue)
            {
                ExpiresAt = CreatedAt.Add(timeout.Value);
            }
            
            ConnectedDevices = new List<DeviceInfo> { serverDevice };
            Status = SessionStatus.Created;
            IsVerified = false;
            EnabledFeatures = SessionFeatures.Default;
        }

        /// <summary>
        /// Adds a device to the session as a client.
        /// </summary>
        /// <param name="device">The device to add as client</param>
        /// <exception cref="InvalidOperationException">Thrown if session is not in a joinable state or is full</exception>
        public void AddClientDevice(DeviceInfo device)
        {
            if (Status != SessionStatus.Created && Status != SessionStatus.WaitingForVerification)
                throw new InvalidOperationException($"Cannot add device to session in {Status} state");
            
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            
            // Check if session is full (server + max clients)
            if (ConnectedDevices.Count >= MaxClients + 1) // +1 for server
                throw new InvalidOperationException($"Session is full. Maximum {MaxClients} clients allowed.");
            
            // Check if device is already connected
            foreach (var connectedDevice in ConnectedDevices)
            {
                if (connectedDevice.Id == device.Id)
                    throw new InvalidOperationException("Device is already connected to this session");
            }
            
            var devices = new List<DeviceInfo>(ConnectedDevices);
            devices.Add(device);
            ConnectedDevices = devices.AsReadOnly();
            
            // After first client joins, move to waiting for verification
            if (ClientDevices.Count == 1)
            {
                Status = SessionStatus.WaitingForVerification;
            }
        }
        
        /// <summary>
        /// Gets the role of a specific device in this session.
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <returns>DeviceRole.Server if it's the server, otherwise DeviceRole.Client</returns>
        public DeviceRole GetDeviceRole(Guid deviceId)
        {
            return deviceId == ServerDevice.Id ? DeviceRole.Server : DeviceRole.Client;
        }
        
        /// <summary>
        /// Checks if a device is the server for this session.
        /// </summary>
        /// <param name="deviceId">The device ID to check</param>
        /// <returns>True if the device is the server</returns>
        public bool IsServerDevice(Guid deviceId)
        {
            return deviceId == ServerDevice.Id;
        }
        
        /// <summary>
        /// Checks if a device is a client in this session.
        /// </summary>
        /// <param name="deviceId">The device ID to check</param>
        /// <returns>True if the device is a client</returns>
        public bool IsClientDevice(Guid deviceId)
        {
            return !IsServerDevice(deviceId) && ConnectedDevices.Any(d => d.Id == deviceId);
        }

        /// <summary>
        /// Removes a device from the session.
        /// </summary>
        /// <param name="deviceId">The ID of the device to remove</param>
        public void RemoveDevice(Guid deviceId)
        {
            var devices = new List<DeviceInfo>(ConnectedDevices);
            devices.RemoveAll(d => d.Id == deviceId);
            ConnectedDevices = devices.AsReadOnly();
            
            // Update status based on remaining devices
            if (ConnectedDevices.Count == 0)
            {
                Status = SessionStatus.Ended;
            }
            else if (ConnectedDevices.Count == 1)
            {
                Status = SessionStatus.Created; // Back to waiting for connections
            }
        }

        /// <summary>
        /// Marks the session as verified.
        /// </summary>
        public void MarkAsVerified()
        {
            if (Status != SessionStatus.WaitingForVerification)
                throw new InvalidOperationException($"Cannot verify session in {Status} state");
            
            IsVerified = true;
            Status = SessionStatus.Active;
        }

        /// <summary>
        /// Updates the enabled features for the session.
        /// </summary>
        /// <param name="features">The new feature set</param>
        public void UpdateFeatures(SessionFeatures features)
        {
            EnabledFeatures = features;
        }

        /// <summary>
        /// Ends the session.
        /// </summary>
        /// <param name="reason">Reason for ending the session</param>
        public void End(string reason = "Session ended")
        {
            Status = SessionStatus.Ended;
            ExpiresAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Checks if the session has expired.
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// Checks if the session is active (verified and not expired).
        /// </summary>
        public bool IsActive => Status == SessionStatus.Active && !IsExpired;

        /// <summary>
        /// Gets the time remaining before expiration.
        /// Returns null if no timeout is set.
        /// </summary>
        public TimeSpan? TimeRemaining
        {
            get
            {
                if (!ExpiresAt.HasValue || IsExpired)
                    return null;
                
                return ExpiresAt.Value - DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Represents the status of a session.
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// Session has been created but no devices have joined yet.
        /// </summary>
        Created,
        
        /// <summary>
        /// A device has joined and we're waiting for manual verification.
        /// </summary>
        WaitingForVerification,
        
        /// <summary>
        /// Session is active and verified.
        /// </summary>
        Active,
        
        /// <summary>
        /// Session has ended.
        /// </summary>
        Ended
    }
}