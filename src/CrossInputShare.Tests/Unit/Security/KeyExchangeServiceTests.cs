using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bogus;
using CrossInputShare.Security.Services;
using CrossInputShare.Tests.TestUtilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CrossInputShare.Tests.Unit.Security
{
    public class KeyExchangeServiceTests : TestBase
    {
        private readonly Faker _faker;

        public KeyExchangeServiceTests(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
            _faker = new Faker();
        }

        [Fact]
        public void Constructor_CreatesInstanceWithKeyPair()
        {
            // Act
            using var service = new KeyExchangeService();

            // Assert
            service.Should().NotBeNull();
            service.PublicKey.Should().NotBeNull();
            service.PublicKey.Should().NotBeEmpty();
        }

        [Fact]
        public void PublicKey_ReturnsValidPublicKey()
        {
            // Arrange
            using var service = new KeyExchangeService();

            // Act
            var publicKey = service.PublicKey;

            // Assert
            publicKey.Should().NotBeNull();
            publicKey.Should().NotBeEmpty();
            publicKey.Length.Should().BeGreaterThan(0);
            
            // Verify it's a valid public key by trying to import it
            using (var testKey = ECDiffieHellman.Create())
            {
                testKey.ImportSubjectPublicKeyInfo(publicKey, out _);
                // No exception means it's valid
            }
        }

        [Fact]
        public void DeriveSharedSecret_ValidRemoteKey_ReturnsSharedSecret()
        {
            // Arrange
            using var alice = new KeyExchangeService();
            using var bob = new KeyExchangeService();
            
            var alicePublicKey = alice.PublicKey;
            var bobPublicKey = bob.PublicKey;

            // Act
            var aliceSharedSecret = alice.DeriveSharedSecret(bobPublicKey);
            var bobSharedSecret = bob.DeriveSharedSecret(alicePublicKey);

            // Assert
            aliceSharedSecret.Should().NotBeNull();
            aliceSharedSecret.Should().NotBeEmpty();
            bobSharedSecret.Should().NotBeNull();
            bobSharedSecret.Should().NotBeEmpty();
            
            // Both should derive the same shared secret
            aliceSharedSecret.Should().Equal(bobSharedSecret);
        }

        [Fact]
        public void DeriveSharedSecret_NullRemoteKey_ThrowsArgumentException()
        {
            // Arrange
            using var service = new KeyExchangeService();

            // Act & Assert
            AssertThrows<ArgumentException>(() => service.DeriveSharedSecret(null));
        }

        [Fact]
        public void DeriveSharedSecret_EmptyRemoteKey_ThrowsArgumentException()
        {
            // Arrange
            using var service = new KeyExchangeService();

            // Act & Assert
            AssertThrows<ArgumentException>(() => service.DeriveSharedSecret(Array.Empty<byte>()));
        }

        [Fact]
        public void DeriveSharedSecret_InvalidRemoteKey_ThrowsCryptographicException()
        {
            // Arrange
            using var service = new KeyExchangeService();
            var invalidKey = Encoding.UTF8.GetBytes("Not a valid public key");

            // Act & Assert
            AssertThrows<CryptographicException>(() => service.DeriveSharedSecret(invalidKey));
        }

        [Fact]
        public void DeriveEncryptionKey_ValidSharedSecret_ReturnsEncryptionKey()
        {
            // Arrange
            using var alice = new KeyExchangeService();
            using var bob = new KeyExchangeService();
            
            var sharedSecret = alice.DeriveSharedSecret(bob.PublicKey);
            var salt = Encoding.UTF8.GetBytes("test-salt");
            var context = Encoding.UTF8.GetBytes("test-context");

            // Act
            var encryptionKey = alice.DeriveEncryptionKey(sharedSecret, salt, context, 32);

            // Assert
            encryptionKey.Should().NotBeNull();
            encryptionKey.Should().NotBeEmpty();
            encryptionKey.Length.Should().Be(32);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(24)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(64)]
        public void DeriveEncryptionKey_DifferentKeyLengths_ReturnsCorrectLength(int keyLength)
        {
            // Arrange
            using var service = new KeyExchangeService();
            var sharedSecret = new byte[32]; // X25519 produces 32-byte shared secret
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(sharedSecret);
            }

            // Act
            var encryptionKey = service.DeriveEncryptionKey(sharedSecret, keyLength: keyLength);

            // Assert
            encryptionKey.Length.Should().Be(keyLength);
        }

        [Theory]
        [InlineData(15)]   // Too short
        [InlineData(65)]   // Too long
        public void DeriveEncryptionKey_InvalidKeyLength_ThrowsArgumentException(int invalidKeyLength)
        {
            // Arrange
            using var service = new KeyExchangeService();
            var sharedSecret = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(sharedSecret);
            }

            // Act & Assert
            AssertThrows<ArgumentException>(() => 
                service.DeriveEncryptionKey(sharedSecret, keyLength: invalidKeyLength));
        }

        [Fact]
        public void DeriveEncryptionKey_NullSharedSecret_ThrowsArgumentException()
        {
            // Arrange
            using var service = new KeyExchangeService();

            // Act & Assert
            AssertThrows<ArgumentException>(() => service.DeriveEncryptionKey(null));
        }

        [Fact]
        public void DeriveEncryptionKey_EmptySharedSecret_ThrowsArgumentException()
        {
            // Arrange
            using var service = new KeyExchangeService();

            // Act & Assert
            AssertThrows<ArgumentException>(() => service.DeriveEncryptionKey(Array.Empty<byte>()));
        }

        [Fact]
        public void DeriveEncryptionKey_WithSaltAndContext_DerivesDifferentKeys()
        {
            // Arrange
            using var service = new KeyExchangeService();
            var sharedSecret = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(sharedSecret);
            }
            
            var salt1 = Encoding.UTF8.GetBytes("salt1");
            var salt2 = Encoding.UTF8.GetBytes("salt2");
            var context1 = Encoding.UTF8.GetBytes("context1");
            var context2 = Encoding.UTF8.GetBytes("context2");

            // Act
            var key1 = service.DeriveEncryptionKey(sharedSecret, salt1, context1);
            var key2 = service.DeriveEncryptionKey(sharedSecret, salt1, context1); // Same params
            var key3 = service.DeriveEncryptionKey(sharedSecret, salt2, context1); // Different salt
            var key4 = service.DeriveEncryptionKey(sharedSecret, salt1, context2); // Different context

            // Assert
            key1.Should().Equal(key2, "same inputs should produce same key");
            key1.Should().NotEqual(key3, "different salt should produce different key");
            key1.Should().NotEqual(key4, "different context should produce different key");
            key3.Should().NotEqual(key4, "different salt and context should produce different key");
        }

        [Fact]
        public void PerformKeyExchange_CompleteExchange_ReturnsEncryptionKey()
        {
            // Arrange
            using var alice = new KeyExchangeService();
            using var bob = new KeyExchangeService();
            
            var salt = Encoding.UTF8.GetBytes("session-salt");
            var context = Encoding.UTF8.GetBytes("session-12345");

            // Act
            var aliceKey = alice.PerformKeyExchange(bob.PublicKey, salt, context);
            var bobKey = bob.PerformKeyExchange(alice.PublicKey, salt, context);

            // Assert
            aliceKey.Should().NotBeNull();
            aliceKey.Should().NotBeEmpty();
            aliceKey.Length.Should().Be(32);
            
            bobKey.Should().NotBeNull();
            bobKey.Should().NotBeEmpty();
            bobKey.Length.Should().Be(32);
            
            // Both should derive the same encryption key
            aliceKey.Should().Equal(bobKey);
        }

        [Fact]
        public void RotateKeyPair_GeneratesNewKeyPair()
        {
            // Arrange
            using var service = new KeyExchangeService();
            var originalPublicKey = service.PublicKey;

            // Act
            service.RotateKeyPair();
            var newPublicKey = service.PublicKey;

            // Assert
            newPublicKey.Should().NotBeNull();
            newPublicKey.Should().NotBeEmpty();
            newPublicKey.Should().NotEqual(originalPublicKey);
        }

        [Fact]
        public void RotateKeyPair_MultipleTimes_WorksCorrectly()
        {
            // Arrange
            using var service = new KeyExchangeService();
            var previousKeys = new List<byte[]>();

            // Act & Assert
            for (int i = 0; i < 5; i++)
            {
                var currentKey = service.PublicKey;
                currentKey.Should().NotBeNull();
                currentKey.Should().NotBeEmpty();
                
                // Should be different from all previous keys
                foreach (var previousKey in previousKeys)
                {
                    currentKey.Should().NotEqual(previousKey);
                }
                
                previousKeys.Add(currentKey);
                service.RotateKeyPair();
            }
        }

        [Fact]
        public void GetKeyPairInfo_ReturnsValidInfo()
        {
            // Arrange
            using var service = new KeyExchangeService();

            // Act
            var info = service.GetKeyPairInfo();

            // Assert
            info.Should().NotBeNull();
            info.CurveName.Should().NotBeNullOrEmpty();
            info.PublicKeyLength.Should().Be(service.PublicKey.Length);
            info.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Dispose_ClearsKeyMaterial()
        {
            // Arrange
            var service = new KeyExchangeService();
            var publicKeyBeforeDispose = service.PublicKey;

            // Act
            service.Dispose();

            // Assert - PublicKey should throw after disposal
            AssertThrows<ObjectDisposedException>(() => _ = service.PublicKey);
            AssertThrows<ObjectDisposedException>(() => service.RotateKeyPair());
            AssertThrows<ObjectDisposedException>(() => service.GetKeyPairInfo());
        }

        [Fact]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var service = new KeyExchangeService();

            // Act
            service.Dispose();
            
            // Assert - Second dispose should not throw
            service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void ForwardSecrecy_NewKeyPair_NewSharedSecret()
        {
            // Arrange
            using var alice = new KeyExchangeService();
            using var bob = new KeyExchangeService();
            
            var bobPublicKey = bob.PublicKey;
            var originalSharedSecret = alice.DeriveSharedSecret(bobPublicKey);

            // Act - Alice rotates her key pair
            alice.RotateKeyPair();
            var newSharedSecret = alice.DeriveSharedSecret(bobPublicKey);

            // Assert
            newSharedSecret.Should().NotBeNull();
            newSharedSecret.Should().NotBeEmpty();
            newSharedSecret.Should().NotEqual(originalSharedSecret);
        }

        [Fact]
        public void KeyExchange_ThreeParty_EachPairHasUniqueSharedSecret()
        {
            // Arrange
            using var alice = new KeyExchangeService();
            using var bob = new KeyExchangeService();
            using var charlie = new KeyExchangeService();

            // Act
            var aliceBobSecret = alice.DeriveSharedSecret(bob.PublicKey);
            var aliceCharlieSecret = alice.DeriveSharedSecret(charlie.PublicKey);
            var bobCharlieSecret = bob.DeriveSharedSecret(charlie.PublicKey);

            // Assert
            aliceBobSecret.Should().NotEqual(aliceCharlieSecret);
            aliceBobSecret.Should().NotEqual(bobCharlieSecret);
            aliceCharlieSecret.Should().NotEqual(bobCharlieSecret);
            
            // Verify symmetry
            var bobAliceSecret = bob.DeriveSharedSecret(alice.PublicKey);
            var charlieAliceSecret = charlie.DeriveSharedSecret(alice.PublicKey);
            var charlieBobSecret = charlie.DeriveSharedSecret(bob.PublicKey);
            
            aliceBobSecret.Should().Equal(bobAliceSecret);
            aliceCharlieSecret.Should().Equal(charlieAliceSecret);
            bobCharlieSecret.Should().Equal(charlieBobSecret);
        }

        [Fact]
        public void Performance_DeriveSharedSecret100Times_LessThan1Second()
        {
            // Arrange
            using var alice = new KeyExchangeService();
            using var bob = new KeyExchangeService();
            var bobPublicKey = bob.PublicKey;
            const int iterations = 100;

            // Act
            var executionTime = MeasureExecutionTime(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    _ = alice.DeriveSharedSecret(bobPublicKey);
                }
            });

            // Assert
            executionTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
            TestOutputHelper.WriteLine($"Derived {iterations} shared secrets in {executionTime.TotalMilliseconds}ms");
            TestOutputHelper.WriteLine($"Average: {executionTime.TotalMilliseconds / iterations:F2}ms per operation");
        }

        [Fact]
        public async Task ThreadSafety_MultipleThreads_PerformKeyExchange()
        {
            // Arrange
            const int threadCount = 5;
            const int exchangesPerThread = 20;
            var exceptions = new ConcurrentBag<Exception>();

            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
            {
                for (int i = 0; i < exchangesPerThread; i++)
                {
                    try
                    {
                        using var alice = new KeyExchangeService();
                        using var bob = new KeyExchangeService();
                        
                        var aliceKey = alice.PerformKeyExchange(bob.PublicKey);
                        var bobKey = bob.PerformKeyExchange(alice.PublicKey);
                        
                        if (!aliceKey.SequenceEqual(bobKey))
                        {
                            exceptions.Add(new Exception($"Thread {threadId}: Key mismatch"));
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
            TestOutputHelper.WriteLine($"Completed {threadCount * exchangesPerThread} key exchanges across {threadCount} threads");
        }

        [Fact]
        public void HKDF_Implementation_ProducesDeterministicOutput()
        {
            // This test verifies the custom HKDF implementation produces consistent results
            // Arrange
            using var service = new KeyExchangeService();
            var sharedSecret = Encoding.UTF8.GetBytes("test-shared-secret-32-bytes-long!!");
            var salt = Encoding.UTF8.GetBytes("test-salt");
            var context = Encoding.UTF8.GetBytes("test-context");

            // Act - Derive same key multiple times
            var key1 = service.DeriveEncryptionKey(sharedSecret, salt, context);
            var key2 = service.DeriveEncryptionKey(sharedSecret, salt, context);
            var key3 = service.DeriveEncryptionKey(sharedSecret, salt, context);

            // Assert
            key1.Should().Equal(key2);
            key1.Should().Equal(key3);
            key2.Should().Equal(key3);
        }

        [Fact]
        public void HKDF_Implementation_DifferentInputs_DifferentOutputs()
        {
            // Arrange
            using var service = new KeyExchangeService();
            var sharedSecret1 = Encoding.UTF8.GetBytes("shared-secret-1-32-bytes-long!!!");
            var sharedSecret2 = Encoding.UTF8.GetBytes("shared-secret-2-32-bytes-long!!!");
            
            // Act
            var key1 = service.DeriveEncryptionKey(sharedSecret1);
            var key2 = service.DeriveEncryptionKey(sharedSecret2);

            // Assert
            key1.Should().NotEqual(key2);
        }

        [Fact]
        public void Curve25519_KeySize_Is256Bits()
        {
            // Arrange
            using var service = new KeyExchangeService();

            // Act
            var sharedSecret = new byte[32]; // Simulated shared secret
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(sharedSecret);
            }
            var encryptionKey = service.DeriveEncryptionKey(sharedSecret);

            // Assert - X25519 produces 32-byte (256-bit) shared secrets
            // and our default encryption key is 32 bytes (256-bit)
            encryptionKey.Length.Should().Be(32);
            
            // Public key should be a reasonable size for X25519
            // (typically around 32 bytes for the raw key, plus ASN.1 encoding overhead)
            service.PublicKey.Length.Should().BeGreaterThan(30).And.BeLessThan(100);
        }
    }
}