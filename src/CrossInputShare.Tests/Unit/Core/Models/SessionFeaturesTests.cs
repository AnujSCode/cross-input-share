using System;
using Bogus;
using CrossInputShare.Core.Models;
using CrossInputShare.Tests.TestUtilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CrossInputShare.Tests.Unit.Core.Models
{
    public class SessionFeaturesTests : TestBase
    {
        private readonly Faker _faker;

        public SessionFeaturesTests(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
            _faker = new Faker();
        }

        [Fact]
        public void Default_ReturnsKeyboardMouseClipboard()
        {
            // Act
            var defaultFeatures = SessionFeaturesExtensions.Default;

            // Assert
            defaultFeatures.Should().HaveFlag(SessionFeatures.Keyboard);
            defaultFeatures.Should().HaveFlag(SessionFeatures.Mouse);
            defaultFeatures.Should().HaveFlag(SessionFeatures.Clipboard);
            defaultFeatures.Should().NotHaveFlag(SessionFeatures.Screen);
            defaultFeatures.Should().NotHaveFlag(SessionFeatures.FileTransfer);
            defaultFeatures.Should().NotHaveFlag(SessionFeatures.Audio);
        }

        [Fact]
        public void All_ReturnsAllFeatures()
        {
            // Act
            var allFeatures = SessionFeaturesExtensions.All;

            // Assert
            allFeatures.Should().HaveFlag(SessionFeatures.Keyboard);
            allFeatures.Should().HaveFlag(SessionFeatures.Mouse);
            allFeatures.Should().HaveFlag(SessionFeatures.Clipboard);
            allFeatures.Should().HaveFlag(SessionFeatures.Screen);
            allFeatures.Should().HaveFlag(SessionFeatures.FileTransfer);
            allFeatures.Should().HaveFlag(SessionFeatures.Audio);
        }

        [Fact]
        public void BasicInput_ReturnsKeyboardAndMouse()
        {
            // Act
            var basicInput = SessionFeaturesExtensions.BasicInput;

            // Assert
            basicInput.Should().HaveFlag(SessionFeatures.Keyboard);
            basicInput.Should().HaveFlag(SessionFeatures.Mouse);
            basicInput.Should().NotHaveFlag(SessionFeatures.Clipboard);
            basicInput.Should().NotHaveFlag(SessionFeatures.Screen);
            basicInput.Should().NotHaveFlag(SessionFeatures.FileTransfer);
            basicInput.Should().NotHaveFlag(SessionFeatures.Audio);
        }

        [Fact]
        public void HasFeature_FeatureEnabled_ReturnsTrue()
        {
            // Arrange
            var features = SessionFeatures.Keyboard | SessionFeatures.Mouse;

            // Act & Assert
            features.HasFeature(SessionFeatures.Keyboard).Should().BeTrue();
            features.HasFeature(SessionFeatures.Mouse).Should().BeTrue();
            features.HasFeature(SessionFeatures.Clipboard).Should().BeFalse();
        }

        [Fact]
        public void HasFeature_MultipleFeatures_ChecksCorrectly()
        {
            // Arrange
            var features = SessionFeatures.Keyboard | SessionFeatures.Mouse | SessionFeatures.Clipboard;

            // Act & Assert
            features.HasFeature(SessionFeatures.Keyboard | SessionFeatures.Mouse).Should().BeTrue();
            features.HasFeature(SessionFeatures.Keyboard | SessionFeatures.Screen).Should().BeFalse();
        }

        [Fact]
        public void Enable_AddsFeature()
        {
            // Arrange
            var features = SessionFeatures.Keyboard;

            // Act
            var enabled = features.Enable(SessionFeatures.Mouse);

            // Assert
            enabled.Should().HaveFlag(SessionFeatures.Keyboard);
            enabled.Should().HaveFlag(SessionFeatures.Mouse);
            enabled.Should().NotHaveFlag(SessionFeatures.Clipboard);
        }

        [Fact]
        public void Enable_AlreadyEnabled_NoChange()
        {
            // Arrange
            var features = SessionFeatures.Keyboard | SessionFeatures.Mouse;

            // Act
            var enabled = features.Enable(SessionFeatures.Keyboard);

            // Assert
            enabled.Should().Be(features);
        }

        [Fact]
        public void Disable_RemovesFeature()
        {
            // Arrange
            var features = SessionFeatures.Keyboard | SessionFeatures.Mouse;

            // Act
            var disabled = features.Disable(SessionFeatures.Mouse);

            // Assert
            disabled.Should().HaveFlag(SessionFeatures.Keyboard);
            disabled.Should().NotHaveFlag(SessionFeatures.Mouse);
        }

        [Fact]
        public void Disable_NotEnabled_NoChange()
        {
            // Arrange
            var features = SessionFeatures.Keyboard;

            // Act
            var disabled = features.Disable(SessionFeatures.Mouse);

            // Assert
            disabled.Should().Be(features);
        }

        [Fact]
        public void Toggle_EnabledFeature_DisablesIt()
        {
            // Arrange
            var features = SessionFeatures.Keyboard | SessionFeatures.Mouse;

            // Act
            var toggled = features.Toggle(SessionFeatures.Mouse);

            // Assert
            toggled.Should().HaveFlag(SessionFeatures.Keyboard);
            toggled.Should().NotHaveFlag(SessionFeatures.Mouse);
        }

        [Fact]
        public void Toggle_DisabledFeature_EnablesIt()
        {
            // Arrange
            var features = SessionFeatures.Keyboard;

            // Act
            var toggled = features.Toggle(SessionFeatures.Mouse);

            // Assert
            toggled.Should().HaveFlag(SessionFeatures.Keyboard);
            toggled.Should().HaveFlag(SessionFeatures.Mouse);
        }

        [Fact]
        public void GetDescription_NoFeatures_ReturnsNone()
        {
            // Arrange
            var features = SessionFeatures.None;

            // Act
            var description = features.GetDescription();

            // Assert
            description.Should().Be("None");
        }

        [Fact]
        public void GetDescription_SingleFeature_ReturnsFeatureName()
        {
            // Arrange
            var features = SessionFeatures.Keyboard;

            // Act
            var description = features.GetDescription();

            // Assert
            description.Should().Be("Keyboard");
        }

        [Fact]
        public void GetDescription_MultipleFeatures_ReturnsCommaSeparatedList()
        {
            // Arrange
            var features = SessionFeatures.Keyboard | SessionFeatures.Mouse | SessionFeatures.Clipboard;

            // Act
            var description = features.GetDescription();

            // Assert
            description.Should().Be("Keyboard, Mouse, Clipboard");
        }

        [Fact]
        public void GetDescription_AllFeatures_ReturnsAllNames()
        {
            // Arrange
            var features = SessionFeaturesExtensions.All;

            // Act
            var description = features.GetDescription();

            // Assert
            description.Should().Be("Keyboard, Mouse, Clipboard, Screen, File Transfer, Audio");
        }

        [Fact]
        public void IsValidForSession_UnverifiedSession_AllowsBasicFeatures()
        {
            // Arrange
            var basicFeatures = SessionFeatures.Keyboard | SessionFeatures.Mouse | SessionFeatures.Clipboard;
            var sensitiveFeatures = SessionFeatures.Screen | SessionFeatures.FileTransfer;

            // Act & Assert
            basicFeatures.IsValidForSession(isSessionVerified: false).Should().BeTrue();
            sensitiveFeatures.IsValidForSession(isSessionVerified: false).Should().BeFalse();
            
            // Mixed features with sensitive ones should fail
            var mixedFeatures = basicFeatures | SessionFeatures.Screen;
            mixedFeatures.IsValidForSession(isSessionVerified: false).Should().BeFalse();
        }

        [Fact]
        public void IsValidForSession_VerifiedSession_AllowsAllFeatures()
        {
            // Arrange
            var allFeatures = SessionFeaturesExtensions.All;

            // Act & Assert
            allFeatures.IsValidForSession(isSessionVerified: true).Should().BeTrue();
        }

        [Fact]
        public void IsValidForSession_AudioFeature_RequiresNoSpecialVerification()
        {
            // Arrange
            var audioOnly = SessionFeatures.Audio;
            var audioWithKeyboard = SessionFeatures.Audio | SessionFeatures.Keyboard;

            // Act & Assert
            audioOnly.IsValidForSession(isSessionVerified: false).Should().BeTrue();
            audioWithKeyboard.IsValidForSession(isSessionVerified: false).Should().BeTrue();
        }

        [Fact]
        public void FlagEnum_Operations_WorkCorrectly()
        {
            // This test verifies basic flag enum operations work as expected
            // Arrange
            var keyboard = SessionFeatures.Keyboard;
            var mouse = SessionFeatures.Mouse;
            var clipboard = SessionFeatures.Clipboard;

            // Act
            var combined = keyboard | mouse | clipboard;
            var withoutMouse = combined & ~mouse;
            var keyboardOnly = combined & keyboard;

            // Assert
            combined.Should().HaveFlag(SessionFeatures.Keyboard);
            combined.Should().HaveFlag(SessionFeatures.Mouse);
            combined.Should().HaveFlag(SessionFeatures.Clipboard);
            
            withoutMouse.Should().HaveFlag(SessionFeatures.Keyboard);
            withoutMouse.Should().NotHaveFlag(SessionFeatures.Mouse);
            withoutMouse.Should().HaveFlag(SessionFeatures.Clipboard);
            
            keyboardOnly.Should().Be(SessionFeatures.Keyboard);
        }

        [Fact]
        public void EnumValues_CorrectBitPositions()
        {
            // Verify each feature is at the correct bit position
            ((int)SessionFeatures.None).Should().Be(0);
            ((int)SessionFeatures.Keyboard).Should().Be(1);      // 2^0
            ((int)SessionFeatures.Mouse).Should().Be(2);         // 2^1
            ((int)SessionFeatures.Clipboard).Should().Be(4);     // 2^2
            ((int)SessionFeatures.Screen).Should().Be(8);        // 2^3
            ((int)SessionFeatures.FileTransfer).Should().Be(16); // 2^4
            ((int)SessionFeatures.Audio).Should().Be(32);        // 2^5
        }

        [Fact]
        public void Combination_UniqueValues()
        {
            // Verify combinations produce unique values
            var combos = new HashSet<int>();
            
            // Test all possible combinations of 3 features
            var allFeatures = new[] 
            {
                SessionFeatures.Keyboard,
                SessionFeatures.Mouse,
                SessionFeatures.Clipboard,
                SessionFeatures.Screen,
                SessionFeatures.FileTransfer,
                SessionFeatures.Audio
            };
            
            // Generate all combinations
            for (int mask = 0; mask < (1 << allFeatures.Length); mask++)
            {
                SessionFeatures combo = SessionFeatures.None;
                for (int i = 0; i < allFeatures.Length; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        combo |= allFeatures[i];
                    }
                }
                combos.Add((int)combo);
            }
            
            // Should have 2^6 = 64 unique combinations
            combos.Count.Should().Be(64);
        }

        [Fact]
        public void ExtensionMethods_ChainCorrectly()
        {
            // Arrange
            var features = SessionFeatures.None;

            // Act
            var result = features
                .Enable(SessionFeatures.Keyboard)
                .Enable(SessionFeatures.Mouse)
                .Toggle(SessionFeatures.Clipboard)
                .Disable(SessionFeatures.Mouse);

            // Assert
            result.Should().HaveFlag(SessionFeatures.Keyboard);
            result.Should().NotHaveFlag(SessionFeatures.Mouse);
            result.Should().HaveFlag(SessionFeatures.Clipboard);
        }

        [Fact]
        public void Performance_Operations_AreFast()
        {
            // Arrange
            const int iterations = 1000000;
            var features = SessionFeatures.Keyboard | SessionFeatures.Mouse;
            
            // Act
            var executionTime = MeasureExecutionTime(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    var temp = features
                        .Enable(SessionFeatures.Clipboard)
                        .Disable(SessionFeatures.Mouse)
                        .Toggle(SessionFeatures.Screen)
                        .HasFeature(SessionFeatures.Keyboard);
                }
            });

            // Assert
            executionTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
            TestOutputHelper.WriteLine($"Completed {iterations} operations in {executionTime.TotalMilliseconds}ms");
            TestOutputHelper.WriteLine($"Average: {executionTime.TotalMilliseconds / iterations * 1000:F2}μs per operation");
        }
    }
}