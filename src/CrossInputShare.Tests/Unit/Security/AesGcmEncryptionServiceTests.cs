using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using CrossInputShare.Security.Services;
using CrossInputShare.Tests.TestUtilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CrossInputShare.Tests.Unit.Security
{
    public class AesGcmEncryptionServiceTests : TestBase
    {
        private readonly Faker _faker;
        private readonly byte[] _testKey256;
        private readonly byte[] _testKey192;
        private readonly byte[] _testKey128;

        public AesGcmEncryptionServiceTests(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
            _faker = new Faker();
            
            // Generate test keys of different sizes
            using (var rng = RandomNumberGenerator.Create())
            {
                _testKey256 = new byte[32]; // AES-256
                _testKey192 = new byte[24]; // AES-192
                _testKey128 = new byte[16]; // AES-128
                
                rng.GetBytes(_testKey256);
                rng.GetBytes(_testKey192);
                rng.GetBytes(_testKey128);
            }
        }

        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            using var service = new AesGcmEncryptionService();

            // Assert
            service.Should().NotBeNull();
            service.IsInitialized.Should().BeFalse();
        }

        [Theory]
        [InlineData(16)]  // AES-128
        [InlineData(24)]  // AES-192
        [InlineData(32)]  // AES-256
        public void Initialize_ValidKeySize_InitializesSuccessfully(int keySize)
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            var key = new byte[keySize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }

            // Act
            service.Initialize(key);

            // Assert
            service.IsInitialized.Should().BeTrue();
        }

        [Theory]
        [InlineData(15)]   // Too short
        [InlineData(17)]   // Not standard size
        [InlineData(31)]   // Not standard size
        [InlineData(33)]   // Too long
        [InlineData(0)]    // Empty
        public void Initialize_InvalidKeySize_ThrowsArgumentException(int keySize)
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            var key = new byte[keySize];

            // Act & Assert
            AssertThrows<ArgumentException>(() => service.Initialize(key));
        }

        [Fact]
        public void Initialize_NullKey_ThrowsArgumentNullException()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();

            // Act & Assert
            AssertThrows<ArgumentNullException>(() => service.Initialize(null));
        }

        [Fact]
        public async Task EncryptAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            var plaintext = Encoding.UTF8.GetBytes("Test data");

            // Act & Assert
            await AssertThrowsAsync<InvalidOperationException>(() => 
                service.EncryptAsync(plaintext));
        }

        [Fact]
        public async Task EncryptAsync_NullPlaintext_ThrowsArgumentNullException()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);

            // Act & Assert
            await AssertThrowsAsync<ArgumentNullException>(() => 
                service.EncryptAsync(null));
        }

        [Fact]
        public async Task EncryptAsync_EmptyPlaintext_ReturnsEncryptedData()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var emptyData = Array.Empty<byte>();

            // Act
            var ciphertext = await service.EncryptAsync(emptyData);

            // Assert
            ciphertext.Should().NotBeNull();
            ciphertext.Should().NotBeEmpty();
            ciphertext.Length.Should().Be(12 + 0 + 16); // Nonce + data + tag
        }

        [Theory]
        [InlineData("Hello, World!")]
        [InlineData("")]
        [InlineData("Test with special chars: !@#$%^&*()")]
        [InlineData("Very long string ")] // 1KB of data
        public async Task EncryptDecryptAsync_RoundTrip_Success(string testData)
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var plaintext = Encoding.UTF8.GetBytes(testData);

            // Act
            var ciphertext = await service.EncryptAsync(plaintext);
            var decrypted = await service.DecryptAsync(ciphertext);

            // Assert
            decrypted.Should().Equal(plaintext);
            Encoding.UTF8.GetString(decrypted).Should().Be(testData);
        }

        [Fact]
        public async Task EncryptDecryptAsync_WithAdditionalData_Success()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var plaintext = Encoding.UTF8.GetBytes("Secret message");
            var additionalData = Encoding.UTF8.GetBytes("Context: user@example.com");

            // Act
            var ciphertext = await service.EncryptAsync(plaintext, additionalData);
            var decrypted = await service.DecryptAsync(ciphertext, additionalData);

            // Assert
            decrypted.Should().Equal(plaintext);
        }

        [Fact]
        public async Task EncryptDecryptAsync_WrongAdditionalData_ThrowsCryptographicException()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var plaintext = Encoding.UTF8.GetBytes("Secret message");
            var correctAdditionalData = Encoding.UTF8.GetBytes("Context: user@example.com");
            var wrongAdditionalData = Encoding.UTF8.GetBytes("Context: attacker@evil.com");

            // Act
            var ciphertext = await service.EncryptAsync(plaintext, correctAdditionalData);
            
            // Assert - Decrypting with wrong additional data should fail
            await AssertThrowsAsync<CryptographicException>(() => 
                service.DecryptAsync(ciphertext, wrongAdditionalData));
        }

        [Fact]
        public async Task EncryptAsync_SamePlaintext_DifferentCiphertext()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var plaintext = Encoding.UTF8.GetBytes("Repeat message");
            
            // Act
            var ciphertext1 = await service.EncryptAsync(plaintext);
            var ciphertext2 = await service.EncryptAsync(plaintext);

            // Assert - Different nonces should produce different ciphertexts
            ciphertext1.Should().NotEqual(ciphertext2);
            
            // But both should decrypt to the same plaintext
            var decrypted1 = await service.DecryptAsync(ciphertext1);
            var decrypted2 = await service.DecryptAsync(ciphertext2);
            decrypted1.Should().Equal(plaintext);
            decrypted2.Should().Equal(plaintext);
        }

        [Fact]
        public async Task DecryptAsync_TamperedCiphertext_ThrowsCryptographicException()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var plaintext = Encoding.UTF8.GetBytes("Original message");
            var ciphertext = await service.EncryptAsync(plaintext);
            
            // Tamper with the ciphertext (change one byte)
            ciphertext[20] ^= 0x01;

            // Act & Assert
            await AssertThrowsAsync<CryptographicException>(() => 
                service.DecryptAsync(ciphertext));
        }

        [Fact]
        public async Task DecryptAsync_TooShortCiphertext_ThrowsArgumentException()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var shortCiphertext = new byte[27]; // Less than nonce + tag size (12 + 16 = 28)

            // Act & Assert
            await AssertThrowsAsync<ArgumentException>(() => 
                service.DecryptAsync(shortCiphertext));
        }

        [Fact]
        public async Task EncryptStringAsync_RoundTrip_Success()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var testString = "Hello, encrypted world!";

            // Act
            var ciphertext = await service.EncryptStringAsync(testString);
            var decryptedString = await service.DecryptStringAsync(ciphertext);

            // Assert
            decryptedString.Should().Be(testString);
        }

        [Fact]
        public async Task EncryptStringAsync_NullOrEmpty_ReturnsEncryptedEmptyData()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);

            // Act
            var ciphertextNull = await service.EncryptStringAsync(null);
            var ciphertextEmpty = await service.EncryptStringAsync(string.Empty);
            var decryptedNull = await service.DecryptStringAsync(ciphertextNull);
            var decryptedEmpty = await service.DecryptStringAsync(ciphertextEmpty);

            // Assert
            decryptedNull.Should().BeEmpty();
            decryptedEmpty.Should().BeEmpty();
        }

        [Fact]
        public void GenerateNonce_ReturnsUniqueNonces()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            const int numberOfNonces = 100;
            var nonces = new HashSet<byte[]>(new ByteArrayComparer());

            // Act
            for (int i = 0; i < numberOfNonces; i++)
            {
                nonces.Add(service.GenerateNonce());
            }

            // Assert
            nonces.Should().HaveCount(numberOfNonces, "all nonces should be unique");
        }

        [Fact]
        public void GenerateNonce_ReturnsCorrectSize()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();

            // Act
            var nonce = service.GenerateNonce();

            // Assert
            nonce.Should().NotBeNull();
            nonce.Length.Should().Be(12); // 96 bits for GCM
        }

        [Fact]
        public async Task RotateKeyAsync_ReturnsNewKey()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);

            // Act
            var newKey = await service.RotateKeyAsync();

            // Assert
            newKey.Should().NotBeNull();
            newKey.Length.Should().Be(_testKey256.Length);
            newKey.Should().NotEqual(_testKey256);
        }

        [Fact]
        public async Task RotateKeyAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();

            // Act & Assert
            await AssertThrowsAsync<InvalidOperationException>(() => 
                service.RotateKeyAsync());
        }

        [Fact]
        public async Task CancellationToken_CancelsOperation()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var largeData = new byte[10 * 1024 * 1024]; // 10MB
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(largeData);
            }
            
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(10); // Cancel after 10ms

            // Act & Assert
            await AssertThrowsAsync<TaskCanceledException>(() => 
                service.EncryptAsync(largeData, cancellationToken: cts.Token));
        }

        [Fact]
        public void Dispose_ClearsKeyFromMemory()
        {
            // Arrange
            var service = new AesGcmEncryptionService();
            var originalKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(originalKey);
            }
            service.Initialize(originalKey);

            // Get reference to internal key (using reflection for test)
            var keyField = typeof(AesGcmEncryptionService).GetField("_key", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var keyBeforeDispose = (byte[])keyField.GetValue(service);

            // Act
            service.Dispose();

            // Assert - Key should be null after dispose
            var keyAfterDispose = (byte[])keyField.GetValue(service);
            keyAfterDispose.Should().BeNull();
            
            // Also check that the original key array was cleared
            // Note: We can't verify this easily since we only have a copy
        }

        [Fact]
        public async Task Performance_EncryptDecrypt1MB_LessThan100ms()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            var testData = new byte[1024 * 1024]; // 1MB
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(testData);
            }

            // Act
            var encryptTime = await MeasureExecutionTimeAsync(() => 
                service.EncryptAsync(testData));
            var ciphertext = await service.EncryptAsync(testData);
            var decryptTime = await MeasureExecutionTimeAsync(() => 
                service.DecryptAsync(ciphertext));

            // Assert
            encryptTime.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
            decryptTime.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
            
            TestOutputHelper.WriteLine($"Encrypt 1MB: {encryptTime.TotalMilliseconds:F2}ms");
            TestOutputHelper.WriteLine($"Decrypt 1MB: {decryptTime.TotalMilliseconds:F2}ms");
        }

        [Theory]
        [InlineData(16)]  // AES-128
        [InlineData(24)]  // AES-192
        [InlineData(32)]  // AES-256
        public async Task DifferentKeySizes_AllWorkCorrectly(int keySize)
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            var key = new byte[keySize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            service.Initialize(key);
            
            var testData = Encoding.UTF8.GetBytes($"Test with {keySize * 8}-bit key");

            // Act
            var ciphertext = await service.EncryptAsync(testData);
            var decrypted = await service.DecryptAsync(ciphertext);

            // Assert
            decrypted.Should().Equal(testData);
        }

        [Fact]
        public async Task ThreadSafety_MultipleThreads_NoExceptions()
        {
            // Arrange
            using var service = new AesGcmEncryptionService();
            service.Initialize(_testKey256);
            
            const int threadCount = 10;
            const int operationsPerThread = 100;
            var exceptions = new ConcurrentBag<Exception>();

            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    try
                    {
                        var testData = Encoding.UTF8.GetBytes($"Thread {threadId}, operation {i}");
                        var ciphertext = await service.EncryptAsync(testData);
                        var decrypted = await service.DecryptAsync(ciphertext);
                        
                        if (!testData.SequenceEqual(decrypted))
                        {
                            exceptions.Add(new Exception($"Thread {threadId}: Data mismatch"));
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Assert
            exceptions.Should().BeEmpty();
            TestOutputHelper.WriteLine($"Completed {threadCount * operationsPerThread} operations across {threadCount} threads");
        }

        // Helper class for comparing byte arrays in HashSet
        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y)
            {
                if (x == null || y == null)
                    return x == y;
                
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] obj)
            {
                if (obj == null)
                    return 0;
                
                // Simple hash code calculation
                int hash = 17;
                foreach (byte b in obj)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }
    }
}