using System;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Represents information about a device participating in a session.
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// Unique identifier for the device in this session.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// The device's fingerprint (SHA256 hash of platform+machine+installation IDs).
        /// </summary>
        public DeviceFingerprint Fingerprint { get; }

        /// <summary>
        /// Name of the device (user-defined or auto-detected).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Platform/operating system of the device.
        /// </summary>
        public string Platform { get; }

        /// <summary>
        /// When the device joined the session.
        /// </summary>
        public DateTime JoinedAt { get; }

        /// <summary>
        /// Whether this device is the host (created the session).
        /// </summary>
        public bool IsHost { get; }

        /// <summary>
        /// Creates a new device information instance.
        /// </summary>
        /// <param name="id">Device ID</param>
        /// <param name="fingerprint">Device fingerprint</param>
        /// <param name="name">Device name</param>
        /// <param name="platform">Platform/OS</param>
        /// <param name="isHost">Whether this device is the host</param>
        public DeviceInfo(Guid id, DeviceFingerprint fingerprint, string name, string platform, bool isHost = false)
        {
            Id = id;
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            JoinedAt = DateTime.UtcNow;
            IsHost = isHost;
        }

        /// <summary>
        /// Creates a device info for the local device (this device).
        /// </summary>
        /// <param name="fingerprint">Local device fingerprint</param>
        /// <param name="name">Local device name</param>
        /// <param name="platform">Local platform</param>
        /// <param name="isHost">Whether this device is creating a session</param>
        /// <returns>A new DeviceInfo instance for the local device</returns>
        public static DeviceInfo CreateLocal(DeviceFingerprint fingerprint, string name, string platform, bool isHost = false)
        {
            return new DeviceInfo(Guid.NewGuid(), fingerprint, name, platform, isHost);
        }

        /// <summary>
        /// Equality comparison based on device ID.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is DeviceInfo other && Id == other.Id;
        }

        /// <summary>
        /// Hash code based on device ID.
        /// </summary>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// String representation for debugging.
        /// </summary>
        public override string ToString() => $"{Name} ({Platform}) - {Fingerprint.ShortDisplay}";
    }
}