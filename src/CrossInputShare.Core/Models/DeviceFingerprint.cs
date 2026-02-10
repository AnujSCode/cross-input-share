using System;
using System.Security.Cryptography;
using System.Text;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Represents a unique fingerprint for a device.
    /// Generated from platform information, machine identifiers, and installation IDs.
    /// Used for device authentication and verification.
    /// </summary>
    public class DeviceFingerprint
    {
        private readonly string _fingerprint;

        /// <summary>
        /// Creates a device fingerprint from its string representation.
        /// </summary>
        /// <param name="fingerprint">The SHA256 hash string (64 hex characters)</param>
        /// <exception cref="ArgumentException">Thrown if fingerprint is invalid</exception>
        public DeviceFingerprint(string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException("Fingerprint cannot be null or empty", nameof(fingerprint));
            
            if (fingerprint.Length != 64) // SHA256 produces 64 hex characters
                throw new ArgumentException("Fingerprint must be 64 hex characters (SHA256)", nameof(fingerprint));
            
            // Validate hex format
            foreach (char c in fingerprint)
            {
                if (!char.IsDigit(c) && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F'))
                    throw new ArgumentException("Fingerprint must contain only hex characters", nameof(fingerprint));
            }
            
            _fingerprint = fingerprint.ToUpperInvariant();
        }

        /// <summary>
        /// Gets the fingerprint as a string (SHA256 hash in hex format).
        /// </summary>
        public override string ToString() => _fingerprint;

        /// <summary>
        /// Gets a shortened display version of the fingerprint (first 12 chars).
        /// Useful for UI display where full hash is too long.
        /// 12 characters provide 48 bits of entropy, reducing collision risk.
        /// </summary>
        public string ShortDisplay => _fingerprint.Substring(0, 12);
        
        /// <summary>
        /// Gets a medium-length display version (first 16 chars).
        /// Provides better security than ShortDisplay while still being readable.
        /// </summary>
        public string MediumDisplay => _fingerprint.Substring(0, 16);

        /// <summary>
        /// Equality comparison based on the fingerprint string.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is DeviceFingerprint other && _fingerprint == other._fingerprint;
        }

        /// <summary>
        /// Hash code based on the fingerprint string.
        /// </summary>
        public override int GetHashCode() => _fingerprint.GetHashCode();

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(DeviceFingerprint left, DeviceFingerprint right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(DeviceFingerprint left, DeviceFingerprint right) => !(left == right);

        /// <summary>
        /// Implicit conversion to string for convenience.
        /// </summary>
        public static implicit operator string(DeviceFingerprint fingerprint) => fingerprint?._fingerprint;

        /// <summary>
        /// Generates a device fingerprint from platform and machine information.
        /// Includes a random salt for privacy and uses structured JSON serialization.
        /// </summary>
        /// <param name="platformInfo">Platform-specific information (OS version, architecture, etc.)</param>
        /// <param name="machineId">Machine-specific identifier</param>
        /// <param name="installationId">Installation-specific identifier</param>
        /// <param name="salt">Optional salt for additional privacy. If null, a random salt is generated.</param>
        /// <returns>A new DeviceFingerprint instance</returns>
        public static DeviceFingerprint Generate(string platformInfo, string machineId, string installationId, byte[] salt = null)
        {
            if (string.IsNullOrWhiteSpace(platformInfo))
                throw new ArgumentException("Platform info cannot be null or empty", nameof(platformInfo));
            
            if (string.IsNullOrWhiteSpace(machineId))
                throw new ArgumentException("Machine ID cannot be null or empty", nameof(machineId));
            
            if (string.IsNullOrWhiteSpace(installationId))
                throw new ArgumentException("Installation ID cannot be null or empty", nameof(installationId));

            // Generate random salt if not provided
            salt = salt ?? GenerateRandomSalt();
            
            // Use structured JSON serialization instead of pipe concatenation
            // This ensures consistent formatting and avoids ambiguity
            var fingerprintData = new FingerprintData
            {
                Platform = platformInfo,
                MachineId = machineId,
                InstallationId = installationId,
                Salt = Convert.ToBase64String(salt),
                Version = 1 // Version field for future compatibility
            };
            
            string json = System.Text.Json.JsonSerializer.Serialize(fingerprintData);
            
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                return new DeviceFingerprint(BytesToHex(hashBytes));
            }
        }
        
        /// <summary>
        /// Generates a cryptographically secure random salt.
        /// </summary>
        private static byte[] GenerateRandomSalt()
        {
            byte[] salt = new byte[32]; // 256-bit salt
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        private static string BytesToHex(byte[] bytes)
        {
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:X2}", b);
            }
            return hex.ToString();
        }
    }

    /// <summary>
    /// Data structure for device fingerprint generation.
    /// Uses JSON serialization for consistent formatting.
    /// </summary>
    internal class FingerprintData
    {
        public string Platform { get; set; }
        public string MachineId { get; set; }
        public string InstallationId { get; set; }
        public string Salt { get; set; }
        public int Version { get; set; }
    }
}