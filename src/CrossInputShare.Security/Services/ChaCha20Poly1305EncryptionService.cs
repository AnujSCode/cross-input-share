using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Interfaces;

namespace CrossInputShare.Security.Services
{
    /// <summary>
    /// Implementation of IEncryptionService using ChaCha20-Poly1305.
    /// ChaCha20-Poly1305 is a modern, fast authenticated encryption algorithm.
    /// It provides both confidentiality (encryption) and integrity (authentication).
    /// 
    /// Why ChaCha20-Poly1305?
    /// 1. Fast on both x86 and ARM processors
    /// 2. Constant-time implementation (resistant to timing attacks)
    /// 3. Simpler than AES-GCM, fewer side-channel concerns
    /// 4. Widely supported across platforms
    /// 
    /// Security considerations:
    /// - Never reuse a nonce with the same key
    /// - Key must be 32 bytes (256 bits)
    /// - Nonce must be 12 bytes (96 bits)
    /// - Authentication tag is 16 bytes (128 bits)
    /// </summary>
    public class ChaCha20Poly1305EncryptionService : IEncryptionService
    {
        private const int KeySize = 32; // 256 bits for ChaCha20
        private const int NonceSize = 12; // 96 bits for ChaCha20-Poly1305
        private const int TagSize = 16; // 128 bits for Poly1305
        
        private byte[] _key;
        private readonly RandomNumberGenerator _rng;
        private bool _disposed;

        /// <summary>
        /// Gets whether the service has been initialized with a key.
        /// </summary>
        public bool IsInitialized => _key != null;

        /// <summary>
        /// Initializes a new instance of the ChaCha20Poly1305EncryptionService class.
        /// </summary>
        public ChaCha20Poly1305EncryptionService()
        {
            _rng = RandomNumberGenerator.Create();
        }

        /// <summary>
        /// Initializes the encryption service with a shared secret key.
        /// </summary>
        /// <param name="sharedSecret">The shared secret key (must be 32 bytes)</param>
        /// <exception cref="ArgumentException">Thrown if key is not 32 bytes</exception>
        public void Initialize(byte[] sharedSecret)
        {
            if (sharedSecret == null)
                throw new ArgumentNullException(nameof(sharedSecret));
            
            if (sharedSecret.Length != KeySize)
                throw new ArgumentException($"Key must be {KeySize} bytes (256 bits) for ChaCha20", nameof(sharedSecret));
            
            // Copy the key to prevent external modification
            _key = new byte[KeySize];
            Buffer.BlockCopy(sharedSecret, 0, _key, 0, KeySize);
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
                    
                    // Create the ciphertext buffer: nonce + encrypted data + tag
                    byte[] ciphertext = new byte[NonceSize + plaintext.Length + TagSize];
                    
                    // Copy nonce to the beginning
                    Buffer.BlockCopy(nonce, 0, ciphertext, 0, NonceSize);
                    
                    // Encrypt using ChaCha20Poly1305
                    using (var chaCha = new ChaCha20Poly1305(_key))
                    {
                        // Encrypt the plaintext and get the tag
                        chaCha.Encrypt(nonce, plaintext, ciphertext, NonceSize, additionalData);
                    }
                    
                    return ciphertext;
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
                    // Extract nonce from the beginning of ciphertext
                    byte[] nonce = new byte[NonceSize];
                    Buffer.BlockCopy(ciphertext, 0, nonce, 0, NonceSize);
                    
                    // Calculate plaintext length
                    int plaintextLength = ciphertext.Length - NonceSize - TagSize;
                    if (plaintextLength < 0)
                        throw new ArgumentException("Ciphertext is malformed", nameof(ciphertext));
                    
                    // Create buffer for plaintext
                    byte[] plaintext = new byte[plaintextLength];
                    
                    // Decrypt and verify using ChaCha20Poly1305
                    using (var chaCha = new ChaCha20Poly1305(_key))
                    {
                        chaCha.Decrypt(nonce, ciphertext, NonceSize, plaintextLength, plaintext, 0, additionalData);
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
                // Generate a new random key
                byte[] newKey = new byte[KeySize];
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
        ~ChaCha20Poly1305EncryptionService()
        {
            Dispose(false);
        }
    }
}