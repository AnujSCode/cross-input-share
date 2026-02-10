using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Interfaces;

namespace CrossInputShare.Security.Services
{
    /// <summary>
    /// Implements X25519 key exchange and HKDF key derivation for secure key establishment.
    /// X25519 is a modern, efficient elliptic curve Diffie-Hellman key exchange.
    /// HKDF (HMAC-based Key Derivation Function) securely derives encryption keys from the shared secret.
    /// </summary>
    public class KeyExchangeService : IKeyExchangeService, IDisposable
    {
        private ECDiffieHellman _localKeyPair;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the KeyExchangeService.
        /// Generates a new X25519 key pair for this device.
        /// </summary>
        public KeyExchangeService()
        {
            // Create X25519 key pair using ECDiffieHellman with Curve25519
            _localKeyPair = ECDiffieHellman.Create(ECCurve.NamedCurves.X25519);
        }

        /// <summary>
        /// Gets the public key for this device.
        /// This should be shared with other devices to establish a shared secret.
        /// </summary>
        public byte[] PublicKey
        {
            get
            {
                ThrowIfDisposed();
                return _localKeyPair.PublicKey.ExportSubjectPublicKeyInfo();
            }
        }

        /// <summary>
        /// Derives a shared secret from a remote public key.
        /// Uses X25519 key exchange to compute the shared secret.
        /// </summary>
        /// <param name="remotePublicKey">The remote device's public key</param>
        /// <returns>The raw shared secret (32 bytes for X25519)</returns>
        /// <exception cref="ArgumentException">Thrown if remote public key is invalid</exception>
        /// <exception cref="CryptographicException">Thrown if key exchange fails</exception>
        public byte[] DeriveSharedSecret(byte[] remotePublicKey)
        {
            ThrowIfDisposed();

            if (remotePublicKey == null || remotePublicKey.Length == 0)
                throw new ArgumentException("Remote public key cannot be null or empty", nameof(remotePublicKey));

            try
            {
                // Import the remote public key
                using (var remoteKey = ECDiffieHellman.Create())
                {
                    remoteKey.ImportSubjectPublicKeyInfo(remotePublicKey, out _);
                    
                    // Perform key exchange to get raw shared secret
                    return _localKeyPair.DeriveKeyMaterial(remoteKey.PublicKey);
                }
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException("Failed to derive shared secret. Invalid remote public key.", ex);
            }
        }

        /// <summary>
        /// Derives an encryption key from a shared secret using HKDF.
        /// HKDF provides secure key derivation with optional context information.
        /// </summary>
        /// <param name="sharedSecret">The raw shared secret from X25519</param>
        /// <param name="salt">Optional salt for HKDF (recommended for key rotation)</param>
        /// <param name="context">Optional context information (e.g., session ID, purpose)</param>
        /// <param name="keyLength">Desired key length in bytes (default: 32 for ChaCha20/AES-256)</param>
        /// <returns>Derived encryption key</returns>
        /// <exception cref="ArgumentException">Thrown if shared secret is invalid</exception>
        public byte[] DeriveEncryptionKey(byte[] sharedSecret, byte[] salt = null, byte[] context = null, int keyLength = 32)
        {
            ThrowIfDisposed();

            if (sharedSecret == null || sharedSecret.Length == 0)
                throw new ArgumentException("Shared secret cannot be null or empty", nameof(sharedSecret));

            if (keyLength < 16 || keyLength > 64)
                throw new ArgumentException("Key length must be between 16 and 64 bytes", nameof(keyLength));

            // Use HKDF to derive a secure encryption key from the shared secret
            using (var hkdf = new Rfc5869DeriveBytes(sharedSecret, salt, context, keyLength))
            {
                return hkdf.GetBytes(keyLength);
            }
        }

        /// <summary>
        /// Performs a complete key exchange and derives an encryption key in one operation.
        /// This is a convenience method that combines DeriveSharedSecret and DeriveEncryptionKey.
        /// </summary>
        /// <param name="remotePublicKey">The remote device's public key</param>
        /// <param name="salt">Optional salt for HKDF</param>
        /// <param name="context">Optional context information</param>
        /// <param name="keyLength">Desired key length in bytes</param>
        /// <returns>Derived encryption key ready for use</returns>
        public byte[] PerformKeyExchange(byte[] remotePublicKey, byte[] salt = null, byte[] context = null, int keyLength = 32)
        {
            byte[] sharedSecret = DeriveSharedSecret(remotePublicKey);
            return DeriveEncryptionKey(sharedSecret, salt, context, keyLength);
        }

        /// <summary>
        /// Generates a new key pair for forward secrecy.
        /// Call this periodically to rotate keys.
        /// </summary>
        public void RotateKeyPair()
        {
            ThrowIfDisposed();
            
            // Dispose old key pair
            _localKeyPair?.Dispose();
            
            // Generate new X25519 key pair
            _localKeyPair = ECDiffieHellman.Create(ECCurve.NamedCurves.X25519);
        }

        /// <summary>
        /// Gets information about the current key pair.
        /// Useful for debugging and logging (does not expose private key).
        /// </summary>
        public KeyPairInfo GetKeyPairInfo()
        {
            ThrowIfDisposed();
            
            var parameters = _localKeyPair.ExportParameters(false); // false = public key only
            return new KeyPairInfo
            {
                CurveName = _localKeyPair.KeyExchangeAlgorithm,
                PublicKeyLength = PublicKey.Length,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Disposes the key pair, clearing it from memory.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _localKeyPair?.Dispose();
                _localKeyPair = null;
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KeyExchangeService));
        }

        /// <summary>
        /// Custom HKDF implementation since .NET doesn't have built-in Rfc5869DeriveBytes in all versions.
        /// This is a simplified implementation for demonstration.
        /// In production, use a well-tested library like BouncyCastle or Microsoft's Security.Cryptography.
        /// </summary>
        private class Rfc5869DeriveBytes : IDisposable
        {
            private readonly HMACSHA256 _hmac;
            private readonly byte[] _info;
            private readonly int _keyLength;
            private byte[] _prk;
            private int _counter = 1;
            private bool _disposed = false;

            public Rfc5869DeriveBytes(byte[] ikm, byte[] salt, byte[] info, int keyLength)
            {
                if (ikm == null) throw new ArgumentNullException(nameof(ikm));
                
                // Use default salt if none provided
                salt = salt ?? new byte[32]; // 32 bytes of zeros
                
                // Extract phase: PRK = HMAC-Hash(salt, IKM)
                using (var hmacExtract = new HMACSHA256(salt))
                {
                    _prk = hmacExtract.ComputeHash(ikm);
                }
                
                // Initialize HMAC for expand phase
                _hmac = new HMACSHA256(_prk);
                _info = info ?? Array.Empty<byte>();
                _keyLength = keyLength;
            }

            public byte[] GetBytes(int byteCount)
            {
                if (byteCount != _keyLength)
                    throw new InvalidOperationException($"Requested {byteCount} bytes but initialized for {_keyLength} bytes");
                
                var result = new byte[byteCount];
                int offset = 0;
                
                // Expand phase: T(N) = HMAC-Hash(PRK, T(N-1) | info | N)
                byte[] previousT = Array.Empty<byte>();
                
                while (offset < byteCount)
                {
                    // Prepare input: T(N-1) | info | N
                    var input = new byte[previousT.Length + _info.Length + 1];
                    Buffer.BlockCopy(previousT, 0, input, 0, previousT.Length);
                    Buffer.BlockCopy(_info, 0, input, previousT.Length, _info.Length);
                    input[input.Length - 1] = (byte)_counter;
                    
                    // Compute T(N)
                    previousT = _hmac.ComputeHash(input);
                    
                    // Copy to result
                    int bytesToCopy = Math.Min(previousT.Length, byteCount - offset);
                    Buffer.BlockCopy(previousT, 0, result, offset, bytesToCopy);
                    offset += bytesToCopy;
                    _counter++;
                }
                
                return result;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _hmac?.Dispose();
                    Array.Clear(_prk, 0, _prk.Length);
                    _disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// Information about a key pair (does not expose private key).
    /// </summary>
    public class KeyPairInfo
    {
        /// <summary>
        /// The name of the elliptic curve used.
        /// </summary>
        public string CurveName { get; set; }
        
        /// <summary>
        /// The length of the public key in bytes.
        /// </summary>
        public int PublicKeyLength { get; set; }
        
        /// <summary>
        /// When the key pair was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Interface for key exchange services.
    /// </summary>
    public interface IKeyExchangeService : IDisposable
    {
        /// <summary>
        /// Gets the public key for this device.
        /// </summary>
        byte[] PublicKey { get; }
        
        /// <summary>
        /// Derives a shared secret from a remote public key.
        /// </summary>
        byte[] DeriveSharedSecret(byte[] remotePublicKey);
        
        /// <summary>
        /// Derives an encryption key from a shared secret using HKDF.
        /// </summary>
        byte[] DeriveEncryptionKey(byte[] sharedSecret, byte[] salt = null, byte[] context = null, int keyLength = 32);
        
        /// <summary>
        /// Performs a complete key exchange and derives an encryption key.
        /// </summary>
        byte[] PerformKeyExchange(byte[] remotePublicKey, byte[] salt = null, byte[] context = null, int keyLength = 32);
        
        /// <summary>
        /// Generates a new key pair for forward secrecy.
        /// </summary>
        void RotateKeyPair();
        
        /// <summary>
        /// Gets information about the current key pair.
        /// </summary>
        KeyPairInfo GetKeyPairInfo();
    }
}