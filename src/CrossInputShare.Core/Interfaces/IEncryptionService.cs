using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossInputShare.Core.Interfaces
{
    /// <summary>
    /// Provides encryption and decryption services using ChaCha20-Poly1305.
    /// ChaCha20-Poly1305 is chosen for its speed, security, and wide platform support.
    /// This is an authenticated encryption algorithm that provides both confidentiality and integrity.
    /// </summary>
    public interface IEncryptionService : IDisposable
    {
        /// <summary>
        /// Gets whether the service has been initialized with a key.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initializes the encryption service with a shared secret key.
        /// The key should be derived from a key exchange protocol (not implemented here for simplicity).
        /// In a production system, this would use a key exchange like X25519.
        /// </summary>
        /// <param name="sharedSecret">The shared secret key (must be 32 bytes for ChaCha20)</param>
        /// <exception cref="ArgumentException">Thrown if key is invalid</exception>
        void Initialize(byte[] sharedSecret);

        /// <summary>
        /// Encrypts data with additional authenticated data (AAD).
        /// AAD is included in the integrity check but not encrypted.
        /// Useful for including metadata that needs integrity protection but not secrecy.
        /// </summary>
        /// <param name="plaintext">Data to encrypt</param>
        /// <param name="additionalData">Additional authenticated data (optional)</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Encrypted data with authentication tag</returns>
        /// <exception cref="InvalidOperationException">Thrown if service is not initialized</exception>
        Task<byte[]> EncryptAsync(byte[] plaintext, byte[] additionalData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts data and verifies the authentication tag.
        /// </summary>
        /// <param name="ciphertext">Encrypted data with authentication tag</param>
        /// <param name="additionalData">Additional authenticated data (must match what was used during encryption)</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Decrypted plaintext</returns>
        /// <exception cref="InvalidOperationException">Thrown if service is not initialized</exception>
        /// <exception cref="CryptographicException">Thrown if authentication fails (tampering detected)</exception>
        Task<byte[]> DecryptAsync(byte[] ciphertext, byte[] additionalData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypts a string (UTF-8 encoded).
        /// Convenience method for text data.
        /// </summary>
        /// <param name="plaintext">String to encrypt</param>
        /// <param name="additionalData">Additional authenticated data (optional)</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Encrypted data with authentication tag</returns>
        Task<byte[]> EncryptStringAsync(string plaintext, byte[] additionalData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts data to a string (UTF-8 encoded).
        /// Convenience method for text data.
        /// </summary>
        /// <param name="ciphertext">Encrypted data with authentication tag</param>
        /// <param name="additionalData">Additional authenticated data (must match what was used during encryption)</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Decrypted string</returns>
        Task<string> DecryptStringAsync(byte[] ciphertext, byte[] additionalData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a cryptographically random nonce for encryption.
        /// Each encryption operation should use a unique nonce.
        /// </summary>
        /// <returns>A 12-byte random nonce (standard for ChaCha20-Poly1305)</returns>
        byte[] GenerateNonce();

        /// <summary>
        /// Rotates the encryption key for forward secrecy.
        /// In a production system, this would be called periodically.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>New shared secret (would need to be transmitted securely in real implementation)</returns>
        Task<byte[]> RotateKeyAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Exception thrown when cryptographic operations fail.
    /// This could be due to tampering, incorrect keys, or other security issues.
    /// </summary>
    public class CryptographicException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the CryptographicException class.
        /// </summary>
        /// <param name="message">The error message</param>
        public CryptographicException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the CryptographicException class.
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public CryptographicException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}