using System;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Event arguments for device connection events.
    /// </summary>
    public class DeviceConnectedEventArgs : EventArgs
    {
        public Guid DeviceId { get; }
        public DeviceInfo DeviceInfo { get; }
        public DeviceRole Role { get; }
        public DateTime ConnectedAt { get; }

        public DeviceConnectedEventArgs(Guid deviceId, DeviceInfo deviceInfo, DeviceRole role)
        {
            DeviceId = deviceId;
            DeviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
            Role = role;
            ConnectedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for device disconnection events.
    /// </summary>
    public class DeviceDisconnectedEventArgs : EventArgs
    {
        public Guid DeviceId { get; }
        public DisconnectReason Reason { get; }
        public DateTime DisconnectedAt { get; }

        public DeviceDisconnectedEventArgs(Guid deviceId, DisconnectReason reason)
        {
            DeviceId = deviceId;
            Reason = reason;
            DisconnectedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for screen sharing state changes.
    /// </summary>
    public class ScreenSharingEventArgs : EventArgs
    {
        public Guid SourceDeviceId { get; }
        public Guid[] DestinationDeviceIds { get; }
        public bool IsSharing { get; }
        public DateTime ChangedAt { get; }

        public ScreenSharingEventArgs(Guid sourceDeviceId, Guid[] destinationDeviceIds, bool isSharing)
        {
            SourceDeviceId = sourceDeviceId;
            DestinationDeviceIds = destinationDeviceIds ?? Array.Empty<Guid>();
            IsSharing = isSharing;
            ChangedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Reasons for device disconnection.
    /// </summary>
    public enum DisconnectReason
    {
        /// <summary>
        /// Normal user-initiated disconnection.
        /// </summary>
        UserRequested,

        /// <summary>
        /// Connection timeout or network failure.
        /// </summary>
        ConnectionLost,

        /// <summary>
        /// Session expired or invalidated.
        /// </summary>
        SessionExpired,

        /// <summary>
        /// Authentication or authorization failure.
        /// </summary>
        AuthenticationFailure,

        /// <summary>
        /// Server shutdown or maintenance.
        /// </summary>
        ServerShutdown,

        /// <summary>
        /// Unknown or unspecified reason.
        /// </summary>
        Unknown
    }
}