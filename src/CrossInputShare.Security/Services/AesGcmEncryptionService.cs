using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Interfaces;

namespace CrossInputShare.Security.Services
{
    /// <summary>
    /// Implementation of IEncryptionService using AES-GCM.
    /// AES-GCM is a widely supported authenticated encryption algorithm.
    /// It provides both confidentiality (encryption) and integrity (authentication).
    /// 
    /// Why AES-GCM?
    /// 1. Widely supported across all .NET platforms
    /// 2. Hardware accelerated on modern CPUs (AES-NI)
    /// 3. Standard and well-vetted algorithm
    /// 4. Good performance characteristics
    /// 
    /// Security considerations:
    /// - Never reuse a nonce with the same key
    /// - Key must be 16, 24, or 32 bytes (128, 192, or 256 bits)
    /// - Nonce must be 12 bytes (96 bits) for GCM
    /// - Authentication tag is 16 bytes (128 bits)
    /// </summary>
    public class AesGcmEncryptionService : IEncryptionService
    {
        private const int KeySize = 32; // 256 bits for AES-256
        private const int NonceSize = 12; // 96 bits for GCM
        private const int TagSize = 16; // 128 bits for GCM
        
        private byte[] _key;
        private readonly RandomNumberGenerator _rng;
        private bool _disposed;

        /// <summary>
        /// Gets whether the service has been initialized with a key.
        /// </summary>
        public bool IsInitialized => _key != null;

        /// <summary>
        /// Initializes a new instance of the AesGcmEncryptionService class.
        /// </summary>
        public AesGcmEncryptionService()
        {
            _rng = RandomNumberGenerator.Create();
        }

        /// <summary>
        /// Initializes the encryption service with a shared secret key.
        /// </summary>
        /// <param name="sharedSecret">The shared secret key (16, 24, or 32 bytes)</param>
        /// <exception cref="ArgumentException">Thrown if key is invalid size</exception>
        public void Initialize(byte[] sharedSecret)
        {
            if (sharedSecret == null)
                throw new ArgumentNullException(nameof(sharedSecret));
            
            // Validate key size for AES
            if (sharedSecret.Length != 16 && sharedSecret.Length != 24 && sharedSecret.Length != 32)
                throw new ArgumentException($"Key must be 16, 24, or 32 bytes for AES. Got {sharedSecret.Length} bytes", nameof(sharedSecret));
            
            // Copy the key to prevent external modification
            _key = new byte[sharedSecret.Length];
            Buffer.BlockCopy(sharedSecret, 0, _key, 0, sharedSecret.Length);
        }

        /// <summary>
        /// Encrypts data with additional authenticated data (AAD).
        /// </summary>
        public async Task<byte[]> EncryptAsync(byte[] plaintext, byte[] additionalData = null, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (plaintext == null)
                throw new ArgumentNullException(nameof(plaintext));
            
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                try
                {
                    // Generate a unique nonce for this encryption
                    byte[] nonce = GenerateNonce();
                    
                    // Create buffers for ciphertext and tag
                    byte[] ciphertext = new byte[plaintext.Length];
                    byte[] tag = new byte[TagSize];
                    
                    // Encrypt using AES-GCM
                    using (var aesGcm = new AesGcm(_key, TagSize))
                    {
                        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, additionalData);
                    }
                    
                    // Combine nonce + ciphertext + tag for transmission
                    byte[] result = new byte[NonceSize + ciphertext.Length + TagSize];
                    Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
                    Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
                    Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);
                    
                    return result;
                }
                catch (CryptographicException ex)
                {
                    throw new CryptographicException("Encryption failed", ex);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Decrypts data and verifies the authentication tag.
        /// </summary>
        public async Task<byte[]> DecryptAsync(byte[] ciphertext, byte[] additionalData = null, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (ciphertext == null)
                throw new ArgumentNullException(nameof(ciphertext));
            
            if (ciphertext.Length < NonceSize + TagSize)
                throw new ArgumentException($"Ciphertext too short. Minimum length is {NonceSize + TagSize} bytes", nameof(ciphertext));
            
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                try
                {
                    // Extract components from ciphertext
                    byte[] nonce = new byte[NonceSize];
                    Buffer.BlockCopy(ciphertext, 0, nonce, 0, NonceSize);
                    
                    int dataLength = ciphertext.Length - NonceSize - TagSize;
                    if (dataLength < 0)
                        throw new ArgumentException("Ciphertext is malformed", nameof(ciphertext));
                    
                    byte[] encryptedData = new byte[dataLength];
                    Buffer.BlockCopy(ciphertext, NonceSize, encryptedData, 0, dataLength);
                    
                    byte[] tag = new byte[TagSize];
                    Buffer.BlockCopy(ciphertext, NonceSize + dataLength, tag, 0, TagSize);
                    
                    // Create buffer for plaintext
                    byte[] plaintext = new byte[dataLength];
                    
                    // Decrypt and verify using AES-GCM
                    using (var aesGcm = new AesGcm(_key, TagSize))
                    {
                        aesGcm.Decrypt(nonce, encryptedData, tag, plaintext, additionalData);
                    }
                    
                    return plaintext;
                }
                catch (CryptographicException ex)
                {
                    // This could be due to tampering, wrong key, or corrupted data
                    throw new CryptographicException("Decryption failed. Possible tampering or incorrect key.", ex);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Encrypts a string (UTF-8 encoded).
        /// </summary>
        public async Task<byte[]> EncryptStringAsync(string plaintext, byte[] additionalData = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(plaintext))
                return await EncryptAsync(Array.Empty<byte>(), additionalData, cancellationToken);
            
            byte[] plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            return await EncryptAsync(plaintextBytes, additionalData, cancellationToken);
        }

        /// <summary>
        /// Decrypts data to a string (UTF-8 encoded).
        /// </summary>
        public async Task<string> DecryptStringAsync(byte[] ciphertext, byte[] additionalData = null, CancellationToken cancellationToken = default)
        {
            byte[] plaintextBytes = await DecryptAsync(ciphertext, additionalData, cancellationToken);
            
            if (plaintextBytes.Length == 0)
                return string.Empty;
            
            return System.Text.Encoding.UTF8.GetString(plaintextBytes);
        }

        /// <summary>
        /// Generates a cryptographically random nonce for encryption.
        /// </summary>
        public byte[] GenerateNonce()
        {
            byte[] nonce = new byte[NonceSize];
            _rng.GetBytes(nonce);
            return nonce;
        }

        /// <summary>
        /// Rotates the encryption key for forward secrecy.
        /// In a real implementation, this would involve key exchange.
        /// For now, we generate a new random key (demonstration only).
        /// </summary>
        public async Task<byte[]> RotateKeyAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            
            return await Task.Run(() =>
            {
                // Generate a new random key of the same size
                byte[] newKey = new byte[_key.Length];
                _rng.GetBytes(newKey);
                
                // In a real system, we would:
                // 1. Generate new key material
                // 2. Use key exchange (X25519) to share it
                // 3. Update the key
                
                // For demonstration, just return the new key
                // In production, the caller would need to transmit this securely
                return newKey;
            }, cancellationToken);
        }

        /// <summary>
        /// Ensures the service is initialized before performing operations.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if service is not initialized</exception>
        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("Encryption service must be initialized with a key before use");
        }

        /// <summary>
        /// Disposes of managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clear the key from memory
                    if (_key != null)
                    {
                        Array.Clear(_key, 0, _key.Length);
                        _key = null;
                    }
                    
                    _rng?.Dispose();
                }
                
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are cleaned up.
        /// </summary>
        ~AesGcmEncryptionService()
        {
            Dispose(false);
        }
    }
}