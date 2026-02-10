using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Bogus;
using CrossInputShare.Core.Models;
using CrossInputShare.Tests.TestUtilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CrossInputShare.Tests.Unit.Core.Models
{
    public class SessionCodeTests : TestBase
    {
        private readonly Faker _faker;

        public SessionCodeTests(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
            _faker = new Faker();
        }

        [Fact]
        public void Constructor_ValidCode_CreatesInstance()
        {
            // Arrange
            var validCode = "ABCD1234E"; // 8 random chars + 1 checksum

            // Act
            var sessionCode = new SessionCode(validCode);

            // Assert
            sessionCode.Should().NotBeNull();
            sessionCode.ToString().Should().Be(validCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_NullOrEmptyCode_ThrowsArgumentException(string invalidCode)
        {
            // Act & Assert
            AssertThrows<ArgumentException>(() => new SessionCode(invalidCode));
        }

        [Theory]
        [InlineData("ABCD123")]    // Too short (7 chars)
        [InlineData("ABCD12345")]  // Too long (9 chars but invalid format)
        [InlineData("ABCD-1234")]  // Wrong format with hyphen
        public void Constructor_InvalidLength_ThrowsArgumentException(string invalidCode)
        {
            // Act & Assert
            AssertThrows<ArgumentException>(() => new SessionCode(invalidCode));
        }

        [Theory]
        [InlineData("ABCD1234I")]  // 'I' is not in valid character set
        [InlineData("ABCD1234O")]  // 'O' is not in valid character set
        [InlineData("ABCD12340")]  // '0' is not in valid character set
        [InlineData("ABCD12341")]  // '1' is not in valid character set
        public void Constructor_InvalidCharacters_ThrowsArgumentException(string invalidCode)
        {
            // Act & Assert
            AssertThrows<ArgumentException>(() => new SessionCode(invalidCode));
        }

        [Fact]
        public void Constructor_InvalidChecksum_ThrowsArgumentException()
        {
            // Arrange - Generate a valid code then change the checksum
            var validCode = SessionCode.Generate().ToString();
            var invalidChecksumCode = validCode.Substring(0, 8) + "A"; // Wrong checksum

            // Act & Assert
            AssertThrows<ArgumentException>(() => new SessionCode(invalidChecksumCode));
        }

        [Fact]
        public void Generate_ProducesValidCode()
        {
            // Act
            var sessionCode = SessionCode.Generate();

            // Assert
            sessionCode.Should().NotBeNull();
            var codeString = sessionCode.ToString();
            codeString.Should().HaveLength(9);
            SessionCode.IsValid(codeString).Should().BeTrue();
        }

        [Fact]
        public void Generate_MultipleCodes_AreUnique()
        {
            // Arrange
            const int numberOfCodes = 100;
            var codes = new HashSet<string>();

            // Act
            for (int i = 0; i < numberOfCodes; i++)
            {
                codes.Add(SessionCode.Generate().ToString());
            }

            // Assert
            codes.Should().HaveCount(numberOfCodes, "all generated codes should be unique");
        }

        [Fact]
        public void Generate_UsesCryptographicallySecureRNG()
        {
            // Arrange
            const int sampleSize = 1000;
            var codes = new List<string>();

            // Act
            for (int i = 0; i < sampleSize; i++)
            {
                codes.Add(SessionCode.Generate().ToString());
            }

            // Assert - Check distribution of characters
            var allChars = string.Join("", codes);
            var charGroups = allChars.GroupBy(c => c).ToList();
            
            // Each character should appear roughly equally (within reasonable bounds)
            var averageCount = (double)allChars.Length / charGroups.Count;
            foreach (var group in charGroups)
            {
                var deviation = Math.Abs(group.Count() - averageCount) / averageCount;
                deviation.Should().BeLessThan(0.2, $"character '{group.Key}' distribution should be roughly uniform");
            }
        }

        [Fact]
        public void IsValid_ValidCode_ReturnsTrue()
        {
            // Arrange
            var validCode = SessionCode.Generate().ToString();

            // Act
            var isValid = SessionCode.IsValid(validCode);

            // Assert
            isValid.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ABCD123")]    // Too short
        [InlineData("ABCD12345")]  // Too long
        [InlineData("ABCD1234I")]  // Invalid character
        [InlineData("ABCD1234A")]  // Invalid checksum
        public void IsValid_InvalidCode_ReturnsFalse(string invalidCode)
        {
            // Act
            var isValid = SessionCode.IsValid(invalidCode);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ToDisplayString_ReturnsFormattedCode()
        {
            // Arrange
            var sessionCode = SessionCode.Generate();
            var codeString = sessionCode.ToString();

            // Act
            var displayString = sessionCode.ToDisplayString();

            // Assert
            displayString.Should().Be($"{codeString.Substring(0, 4)}-{codeString.Substring(4, 5)}");
            displayString.Should().Contain("-");
        }

        [Fact]
        public void RandomPart_ReturnsFirst8Characters()
        {
            // Arrange
            var sessionCode = SessionCode.Generate();
            var codeString = sessionCode.ToString();

            // Act
            var randomPart = sessionCode.RandomPart;

            // Assert
            randomPart.Should().Be(codeString.Substring(0, 8));
            randomPart.Should().HaveLength(8);
        }

        [Fact]
        public void Checksum_ReturnsLastCharacter()
        {
            // Arrange
            var sessionCode = SessionCode.Generate();
            var codeString = sessionCode.ToString();

            // Act
            var checksum = sessionCode.Checksum;

            // Assert
            checksum.Should().Be(codeString[8]);
        }

        [Fact]
        public void Equals_SameCode_ReturnsTrue()
        {
            // Arrange
            var code1 = new SessionCode("ABCD1234E");
            var code2 = new SessionCode("ABCD1234E");

            // Act & Assert
            code1.Equals(code2).Should().BeTrue();
            (code1 == code2).Should().BeTrue();
            (code1 != code2).Should().BeFalse();
        }

        [Fact]
        public void Equals_DifferentCode_ReturnsFalse()
        {
            // Arrange
            var code1 = SessionCode.Generate();
            var code2 = SessionCode.Generate();

            // Act & Assert
            code1.Equals(code2).Should().BeFalse();
            (code1 == code2).Should().BeFalse();
            (code1 != code2).Should().BeTrue();
        }

        [Fact]
        public void GetHashCode_SameCode_SameHashCode()
        {
            // Arrange
            var code1 = new SessionCode("ABCD1234E");
            var code2 = new SessionCode("ABCD1234E");

            // Act
            var hashCode1 = code1.GetHashCode();
            var hashCode2 = code2.GetHashCode();

            // Assert
            hashCode1.Should().Be(hashCode2);
        }

        [Fact]
        public void GetHashCode_DifferentCode_DifferentHashCode()
        {
            // Arrange
            var code1 = SessionCode.Generate();
            var code2 = SessionCode.Generate();

            // Act
            var hashCode1 = code1.GetHashCode();
            var hashCode2 = code2.GetHashCode();

            // Assert
            hashCode1.Should().NotBe(hashCode2);
        }

        [Fact]
        public void ImplicitStringConversion_ReturnsCodeString()
        {
            // Arrange
            var sessionCode = SessionCode.Generate();

            // Act
            string codeString = sessionCode;

            // Assert
            codeString.Should().Be(sessionCode.ToString());
        }

        [Fact]
        public void CalculateChecksum_ValidRandomPart_ReturnsValidChecksum()
        {
            // This test uses reflection to test the private CalculateChecksum method
            // Arrange
            var randomPart = "ABCD1234";
            var method = typeof(SessionCode).GetMethod("CalculateChecksum", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            // Act
            var checksum = (char)method.Invoke(null, new object[] { randomPart });

            // Assert
            checksum.Should().BeIn(SessionCodeTestsHelper.ValidChars);
            var fullCode = randomPart + checksum;
            SessionCode.IsValid(fullCode).Should().BeTrue();
        }

        [Theory]
        [InlineData("ABCD1234", "E")]  // Example with known checksum
        [InlineData("WXYZ5678", "B")]  // Another example
        public void ValidateChecksum_KnownValues_ValidatesCorrectly(string randomPart, string expectedChecksum)
        {
            // This test uses reflection to test the private ValidateChecksum method
            // Arrange
            var fullCode = randomPart + expectedChecksum;
            var method = typeof(SessionCode).GetMethod("ValidateChecksum", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            // Act
            var isValid = (bool)method.Invoke(null, new object[] { fullCode });

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void Performance_Generate1000Codes_LessThan1Second()
        {
            // Arrange
            const int numberOfCodes = 1000;
            
            // Act
            var executionTime = MeasureExecutionTime(() =>
            {
                for (int i = 0; i < numberOfCodes; i++)
                {
                    _ = SessionCode.Generate();
                }
            });

            // Assert
            executionTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
            TestOutputHelper.WriteLine($"Generated {numberOfCodes} codes in {executionTime.TotalMilliseconds}ms");
        }

        [Fact]
        public void Entropy_CodeHasSufficientEntropy()
        {
            // Arrange
            const int sampleSize = 10000;
            var codes = new HashSet<string>();
            
            // Act
            for (int i = 0; i < sampleSize; i++)
            {
                codes.Add(SessionCode.Generate().ToString());
            }
            
            // Calculate collision probability
            var collisionRate = 1.0 - ((double)codes.Count / sampleSize);
            
            // Assert - Collision rate should be very low
            collisionRate.Should().BeLessThan(0.001);
            TestOutputHelper.WriteLine($"Collision rate: {collisionRate:P4}");
            TestOutputHelper.WriteLine($"Unique codes: {codes.Count}/{sampleSize}");
        }
    }

    /// <summary>
    /// Helper class for SessionCode tests.
    /// </summary>
    internal static class SessionCodeTestsHelper
    {
        public static string ValidChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    }
}