using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bogus;
using CrossInputShare.Core.Models;
using CrossInputShare.Tests.TestUtilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CrossInputShare.Tests.Unit.Core.Models
{
    public class DeviceFingerprintTests : TestBase
    {
        private readonly Faker _faker;

        public DeviceFingerprintTests(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
            _faker = new Faker();
        }

        [Fact]
        public void Constructor_ValidFingerprint_CreatesInstance()
        {
            // Arrange
            var validFingerprint = new string('A', 64); // 64 hex chars

            // Act
            var fingerprint = new DeviceFingerprint(validFingerprint);

            // Assert
            fingerprint.Should().NotBeNull();
            fingerprint.ToString().Should().Be(validFingerprint.ToUpperInvariant());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_NullOrEmptyFingerprint_ThrowsArgumentException(string invalidFingerprint)
        {
            // Act & Assert
            AssertThrows<ArgumentException>(() => new DeviceFingerprint(invalidFingerprint));
        }

        [Theory]
        [InlineData("ABC")]                    // Too short
        [InlineData(new string('A', 63))]      // 63 chars
        [InlineData(new string('A', 65))]      // 65 chars
        public void Constructor_InvalidLength_ThrowsArgumentException(string invalidFingerprint)
        {
            // Act & Assert
            AssertThrows<ArgumentException>(() => new DeviceFingerprint(invalidFingerprint));
        }

        [Theory]
        [InlineData("GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG")] // 'G' not hex
        [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")] // 'Z' not hex
        [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA ")] // Space not hex
        public void Constructor_InvalidHexCharacters_ThrowsArgumentException(string invalidFingerprint)
        {
            // Act & Assert
            AssertThrows<ArgumentException>(() => new DeviceFingerprint(invalidFingerprint));
        }

        [Fact]
        public void Constructor_MixedCase_NormalizesToUpper()
        {
            // Arrange
            var mixedCaseFingerprint = "abcdef1234567890ABCDEF1234567890abcdef1234567890ABCDEF1234567890";

            // Act
            var fingerprint = new DeviceFingerprint(mixedCaseFingerprint);

            // Assert
            fingerprint.ToString().Should().Be(mixedCaseFingerprint.ToUpperInvariant());
        }

        [Fact]
        public void ShortDisplay_ReturnsFirst12Characters()
        {
            // Arrange
            var fullFingerprint = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
            var fingerprint = new DeviceFingerprint(fullFingerprint);

            // Act
            var shortDisplay = fingerprint.ShortDisplay;

            // Assert
            shortDisplay.Should().Be("0123456789AB");
            shortDisplay.Should().HaveLength(12);
        }

        [Fact]
        public void MediumDisplay_ReturnsFirst16Characters()
        {
            // Arrange
            var fullFingerprint = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
            var fingerprint = new DeviceFingerprint(fullFingerprint);

            // Act
            var mediumDisplay = fingerprint.MediumDisplay;

            // Assert
            mediumDisplay.Should().Be("0123456789ABCDEF");
            mediumDisplay.Should().HaveLength(16);
        }

        [Fact]
        public void Generate_ValidInputs_ReturnsFingerprint()
        {
            // Arrange
            var platformInfo = "Windows 11 22H2 x64";
            var machineId = "MACHINE-12345";
            var installationId = "INSTALL-67890";

            // Act
            var fingerprint = DeviceFingerprint.Generate(platformInfo, machineId, installationId);

            // Assert
            fingerprint.Should().NotBeNull();
            fingerprint.ToString().Should().HaveLength(64);
            fingerprint.ToString().Should().MatchRegex("^[0-9A-F]{64}$");
        }

        [Theory]
        [InlineData(null, "machine", "installation")]
        [InlineData("", "machine", "installation")]
        [InlineData("   ", "machine", "installation")]
        [InlineData("platform", null, "installation")]
        [InlineData("platform", "", "installation")]
        [InlineData("platform", "machine", null)]
        [InlineData("platform", "machine", "")]
        public void Generate_InvalidInputs_ThrowsArgumentException(
            string platformInfo, string machineId, string installationId)
        {
            // Act & Assert
            AssertThrows<ArgumentException>(() => 
                DeviceFingerprint.Generate(platformInfo, machineId, installationId));
        }

        [Fact]
        public void Generate_SameInputsSameSalt_SameFingerprint()
        {
            // Arrange
            var platformInfo = "Ubuntu 22.04 LTS";
            var machineId = "ubuntu-machine-001";
            var installationId = "install-2024-01-01";
            var salt = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Act
            var fingerprint1 = DeviceFingerprint.Generate(platformInfo, machineId, installationId, salt);
            var fingerprint2 = DeviceFingerprint.Generate(platformInfo, machineId, installationId, salt);

            // Assert
            fingerprint1.Should().NotBeNull();
            fingerprint2.Should().NotBeNull();
            fingerprint1.Should().Be(fingerprint2);
            fingerprint1.ToString().Should().Be(fingerprint2.ToString());
        }

        [Fact]
        public void Generate_SameInputsDifferentSalt_DifferentFingerprint()
        {
            // Arrange
            var platformInfo = "Android 14";
            var machineId = "android-device-001";
            var installationId = "install-2024-01-01";
            
            var salt1 = new byte[32];
            var salt2 = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt1);
                salt2[0] = (byte)(salt1[0] ^ 0x01); // Change one byte
            }

            // Act
            var fingerprint1 = DeviceFingerprint.Generate(platformInfo, machineId, installationId, salt1);
            var fingerprint2 = DeviceFingerprint.Generate(platformInfo, machineId, installationId, salt2);

            // Assert
            fingerprint1.Should().NotBeNull();
            fingerprint2.Should().NotBeNull();
            fingerprint1.Should().NotBe(fingerprint2);
            fingerprint1.ToString().Should().NotBe(fingerprint2.ToString());
        }

        [Fact]
        public void Generate_NoSaltProvided_GeneratesRandomSalt()
        {
            // Arrange
            var platformInfo = "Test Platform";
            var machineId = "Test Machine";
            var installationId = "Test Installation";

            // Act
            var fingerprint1 = DeviceFingerprint.Generate(platformInfo, machineId, installationId);
            var fingerprint2 = DeviceFingerprint.Generate(platformInfo, machineId, installationId);

            // Assert - Different salts should produce different fingerprints
            fingerprint1.Should().NotBeNull();
            fingerprint2.Should().NotBeNull();
            fingerprint1.Should().NotBe(fingerprint2);
            fingerprint1.ToString().Should().NotBe(fingerprint2.ToString());
        }

        [Fact]
        public void Generate_DifferentInputs_DifferentFingerprints()
        {
            // Arrange
            var platformInfo1 = "Windows 11";
            var platformInfo2 = "Ubuntu 22.04";
            var machineId = "TEST-MACHINE";
            var installationId = "TEST-INSTALL";
            var salt = new byte[32];

            // Act
            var fingerprint1 = DeviceFingerprint.Generate(platformInfo1, machineId, installationId, salt);
            var fingerprint2 = DeviceFingerprint.Generate(platformInfo2, machineId, installationId, salt);

            // Assert
            fingerprint1.Should().NotBe(fingerprint2);
            fingerprint1.ToString().Should().NotBe(fingerprint2.ToString());
        }

        [Fact]
        public void Equals_SameFingerprint_ReturnsTrue()
        {
            // Arrange
            var fingerprint1 = new DeviceFingerprint(new string('A', 64));
            var fingerprint2 = new DeviceFingerprint(new string('A', 64));

            // Act & Assert
            fingerprint1.Equals(fingerprint2).Should().BeTrue();
            fingerprint1.Equals((object)fingerprint2).Should().BeTrue();
            (fingerprint1 == fingerprint2).Should().BeTrue();
            (fingerprint1 != fingerprint2).Should().BeFalse();
        }

        [Fact]
        public void Equals_DifferentFingerprint_ReturnsFalse()
        {
            // Arrange
            var fingerprint1 = new DeviceFingerprint(new string('A', 64));
            var fingerprint2 = new DeviceFingerprint(new string('B', 64));

            // Act & Assert
            fingerprint1.Equals(fingerprint2).Should().BeFalse();
            fingerprint1.Equals((object)fingerprint2).Should().BeFalse();
            (fingerprint1 == fingerprint2).Should().BeFalse();
            (fingerprint1 != fingerprint2).Should().BeTrue();
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            // Arrange
            var fingerprint = new DeviceFingerprint(new string('A', 64));

            // Act & Assert
            fingerprint.Equals(null).Should().BeFalse();
            fingerprint.Equals((object)null).Should().BeFalse();
        }

        [Fact]
        public void GetHashCode_SameFingerprint_SameHashCode()
        {
            // Arrange
            var fingerprint1 = new DeviceFingerprint(new string('A', 64));
            var fingerprint2 = new DeviceFingerprint(new string('A', 64));

            // Act
            var hashCode1 = fingerprint1.GetHashCode();
            var hashCode2 = fingerprint2.GetHashCode();

            // Assert
            hashCode1.Should().Be(hashCode2);
        }

        [Fact]
        public void GetHashCode_DifferentFingerprint_DifferentHashCode()
        {
            // Arrange
            var fingerprint1 = new DeviceFingerprint(new string('A', 64));
            var fingerprint2 = new DeviceFingerprint(new string('B', 64));

            // Act
            var hashCode1 = fingerprint1.GetHashCode();
            var hashCode2 = fingerprint2.GetHashCode();

            // Assert
            hashCode1.Should().NotBe(hashCode2);
        }

        [Fact]
        public void ImplicitStringConversion_ReturnsFingerprintString()
        {
            // Arrange
            var fingerprint = DeviceFingerprint.Generate("Test", "Machine", "Install");

            // Act
            string fingerprintString = fingerprint;

            // Assert
            fingerprintString.Should().Be(fingerprint.ToString());
        }

        [Fact]
        public void ShortDisplay_CollisionProbability_IsLow()
        {
            // This test verifies that the short display (12 chars) has low collision probability
            // Arrange
            const int numberOfFingerprints = 10000;
            var shortDisplays = new HashSet<string>();

            // Act
            for (int i = 0; i < numberOfFingerprints; i++)
            {
                var fingerprint = DeviceFingerprint.Generate(
                    $"Platform{i}", 
                    $"Machine{i}", 
                    $"Install{i}");
                shortDisplays.Add(fingerprint.ShortDisplay);
            }

            // Calculate collision rate
            var collisionRate = 1.0 - ((double)shortDisplays.Count / numberOfFingerprints);

            // Assert - With 12 hex chars (48 bits), collision probability should be very low
            collisionRate.Should().BeLessThan(0.001);
            TestOutputHelper.WriteLine($"Short display collision rate: {collisionRate:P4}");
            TestOutputHelper.WriteLine($"Unique short displays: {shortDisplays.Count}/{numberOfFingerprints}");
        }

        [Fact]
        public void MediumDisplay_CollisionProbability_VeryLow()
        {
            // This test verifies that the medium display (16 chars) has very low collision probability
            // Arrange
            const int numberOfFingerprints = 10000;
            var mediumDisplays = new HashSet<string>();

            // Act
            for (int i = 0; i < numberOfFingerprints; i++)
            {
                var fingerprint = DeviceFingerprint.Generate(
                    $"Platform{i}", 
                    $"Machine{i}", 
                    $"Install{i}");
                mediumDisplays.Add(fingerprint.MediumDisplay);
            }

            // Calculate collision rate
            var collisionRate = 1.0 - ((double)mediumDisplays.Count / numberOfFingerprints);

            // Assert - With 16 hex chars (64 bits), collision probability should be extremely low
            collisionRate.Should().BeLessThan(0.0001);
            TestOutputHelper.WriteLine($"Medium display collision rate: {collisionRate:P4}");
            TestOutputHelper.WriteLine($"Unique medium displays: {mediumDisplays.Count}/{numberOfFingerprints}");
        }

        [Fact]
        public void Generate_UsesJsonSerialization_ForConsistency()
        {
            // Arrange
            var platformInfo = "Test\\Platform"; // Contains backslash
            var machineId = "Test|Machine";      // Contains pipe
            var installationId = "Test\"Install"; // Contains quote
            var salt = new byte[32];

            // Act - Should not throw due to JSON serialization handling special characters
            var fingerprint = DeviceFingerprint.Generate(platformInfo, machineId, installationId, salt);

            // Assert
            fingerprint.Should().NotBeNull();
            fingerprint.ToString().Should().HaveLength(64);
        }

        [Fact]
        public void Performance_Generate1000Fingerprints_LessThan1Second()
        {
            // Arrange
            const int numberOfFingerprints = 1000;
            
            // Act
            var executionTime = MeasureExecutionTime(() =>
            {
                for (int i = 0; i < numberOfFingerprints; i++)
                {
                    _ = DeviceFingerprint.Generate(
                        $"Platform{i}", 
                        $"Machine{i}", 
                        $"Install{i}");
                }
            });

            // Assert
            executionTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
            TestOutputHelper.WriteLine($"Generated {numberOfFingerprints} fingerprints in {executionTime.TotalMilliseconds}ms");
        }

        [Fact]
        public void Fingerprint_Uniqueness_High()
        {
            // Arrange
            const int numberOfFingerprints = 1000;
            var fingerprints = new HashSet<string>();

            // Act
            for (int i = 0; i < numberOfFingerprints; i++)
            {
                var fingerprint = DeviceFingerprint.Generate(
                    $"Platform{i % 10}", // Only 10 unique platforms
                    $"Machine{i % 100}", // Only 100 unique machines
                    $"Install{i}");
                fingerprints.Add(fingerprint.ToString());
            }

            // Calculate collision rate
            var collisionRate = 1.0 - ((double)fingerprints.Count / numberOfFingerprints);

            // Assert - Even with limited unique inputs, fingerprints should be unique due to salt
            collisionRate.Should().BeLessThan(0.001);
            TestOutputHelper.WriteLine($"Full fingerprint collision rate: {collisionRate:P4}");
            TestOutputHelper.WriteLine($"Unique fingerprints: {fingerprints.Count}/{numberOfFingerprints}");
        }

        [Fact]
        public void Salt_Generation_IsCryptographicallySecure()
        {
            // Arrange
            const int numberOfSalts = 1000;
            var salts = new HashSet<byte[]>(new ByteArrayComparer());

            // Act - Generate salts using the private method via reflection
            var method = typeof(DeviceFingerprint).GetMethod("GenerateRandomSalt", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            for (int i = 0; i < numberOfSalts; i++)
            {
                var salt = (byte[])method.Invoke(null, null);
                salts.Add(salt);
            }

            // Assert - All salts should be unique
            salts.Should().HaveCount(numberOfSalts);
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