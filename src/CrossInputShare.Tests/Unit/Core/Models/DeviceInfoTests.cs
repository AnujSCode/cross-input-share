using System;
using Bogus;
using CrossInputShare.Core.Models;
using CrossInputShare.Tests.TestUtilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CrossInputShare.Tests.Unit.Core.Models
{
    public class DeviceInfoTests : TestBase
    {
        private readonly Faker _faker;

        public DeviceInfoTests(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
            _faker = new Faker();
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var deviceName = "Test Device";
            var platform = "Windows 11";
            var role = DeviceRole.Server;
            var fingerprint = new DeviceFingerprint(new string('A', 64));

            // Act
            var deviceInfo = new DeviceInfo(deviceId, deviceName, platform, role, fingerprint);

            // Assert
            deviceInfo.Should().NotBeNull();
            deviceInfo.DeviceId.Should().Be(deviceId);
            deviceInfo.DeviceName.Should().Be(deviceName);
            deviceInfo.Platform.Should().Be(platform);
            deviceInfo.Role.Should().Be(role);
            deviceInfo.Fingerprint.Should().Be(fingerprint);
            deviceInfo.ConnectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            deviceInfo.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Constructor_NullFingerprint_ThrowsArgumentNullException()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var deviceName = "Test Device";
            var platform = "Windows 11";
            var role = DeviceRole.Server;

            // Act & Assert
            AssertThrows<ArgumentNullException>(() => 
                new DeviceInfo(deviceId, deviceName, platform, role, null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidDeviceName_ThrowsArgumentException(string invalidDeviceName)
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var platform = "Windows 11";
            var role = DeviceRole.Server;
            var fingerprint = new DeviceFingerprint(new string('A', 64));

            // Act & Assert
            AssertThrows<ArgumentException>(() => 
                new DeviceInfo(deviceId, invalidDeviceName, platform, role, fingerprint));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidPlatform_ThrowsArgumentException(string invalidPlatform)
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var deviceName = "Test Device";
            var role = DeviceRole.Server;
            var fingerprint = new DeviceFingerprint(new string('A', 64));

            // Act & Assert
            AssertThrows<ArgumentException>(() => 
                new DeviceInfo(deviceId, deviceName, invalidPlatform, role, fingerprint));
        }

        [Fact]
        public void UpdateLastSeen_UpdatesTimestamp()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            var originalLastSeen = deviceInfo.LastSeenAt;
            
            // Wait a bit to ensure time difference
            System.Threading.Thread.Sleep(10);

            // Act
            deviceInfo.UpdateLastSeen();

            // Assert
            deviceInfo.LastSeenAt.Should().BeAfter(originalLastSeen);
            deviceInfo.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void IsOnline_RecentlyUpdated_ReturnsTrue()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            deviceInfo.UpdateLastSeen();

            // Act
            var isOnline = deviceInfo.IsOnline();

            // Assert
            isOnline.Should().BeTrue();
        }

        [Fact]
        public void IsOnline_NotUpdatedFor5Minutes_ReturnsFalse()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            var oldTimestamp = DateTime.UtcNow.AddMinutes(-6); // 6 minutes ago
            var deviceInfoType = typeof(DeviceInfo);
            var lastSeenField = deviceInfoType.GetField("_lastSeenAt", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            lastSeenField.SetValue(deviceInfo, oldTimestamp);

            // Act
            var isOnline = deviceInfo.IsOnline();

            // Assert
            isOnline.Should().BeFalse();
        }

        [Fact]
        public void UpdateFeatures_ValidFeatures_UpdatesSuccessfully()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            var newFeatures = SessionFeatures.Keyboard | SessionFeatures.Mouse;

            // Act
            deviceInfo.UpdateFeatures(newFeatures);

            // Assert
            deviceInfo.EnabledFeatures.Should().Be(newFeatures);
        }

        [Fact]
        public void EnableFeature_AddsFeature()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            deviceInfo.UpdateFeatures(SessionFeatures.Keyboard);

            // Act
            deviceInfo.EnableFeature(SessionFeatures.Mouse);

            // Assert
            deviceInfo.EnabledFeatures.Should().HaveFlag(SessionFeatures.Keyboard);
            deviceInfo.EnabledFeatures.Should().HaveFlag(SessionFeatures.Mouse);
        }

        [Fact]
        public void DisableFeature_RemovesFeature()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            deviceInfo.UpdateFeatures(SessionFeatures.Keyboard | SessionFeatures.Mouse);

            // Act
            deviceInfo.DisableFeature(SessionFeatures.Mouse);

            // Assert
            deviceInfo.EnabledFeatures.Should().HaveFlag(SessionFeatures.Keyboard);
            deviceInfo.EnabledFeatures.Should().NotHaveFlag(SessionFeatures.Mouse);
        }

        [Fact]
        public void HasFeature_FeatureEnabled_ReturnsTrue()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            deviceInfo.UpdateFeatures(SessionFeatures.Keyboard | SessionFeatures.Clipboard);

            // Act & Assert
            deviceInfo.HasFeature(SessionFeatures.Keyboard).Should().BeTrue();
            deviceInfo.HasFeature(SessionFeatures.Clipboard).Should().BeTrue();
            deviceInfo.HasFeature(SessionFeatures.Mouse).Should().BeFalse();
            deviceInfo.HasFeature(SessionFeatures.Screen).Should().BeFalse();
        }

        [Fact]
        public void Equals_SameDeviceId_ReturnsTrue()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var deviceInfo1 = CreateTestDeviceInfo(deviceId);
            var deviceInfo2 = CreateTestDeviceInfo(deviceId);

            // Act & Assert
            deviceInfo1.Equals(deviceInfo2).Should().BeTrue();
            deviceInfo1.Equals((object)deviceInfo2).Should().BeTrue();
            (deviceInfo1 == deviceInfo2).Should().BeTrue();
            (deviceInfo1 != deviceInfo2).Should().BeFalse();
        }

        [Fact]
        public void Equals_DifferentDeviceId_ReturnsFalse()
        {
            // Arrange
            var deviceInfo1 = CreateTestDeviceInfo(Guid.NewGuid());
            var deviceInfo2 = CreateTestDeviceInfo(Guid.NewGuid());

            // Act & Assert
            deviceInfo1.Equals(deviceInfo2).Should().BeFalse();
            deviceInfo1.Equals((object)deviceInfo2).Should().BeFalse();
            (deviceInfo1 == deviceInfo2).Should().BeFalse();
            (deviceInfo1 != deviceInfo2).Should().BeTrue();
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();

            // Act & Assert
            deviceInfo.Equals(null).Should().BeFalse();
            deviceInfo.Equals((object)null).Should().BeFalse();
        }

        [Fact]
        public void GetHashCode_SameDeviceId_SameHashCode()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var deviceInfo1 = CreateTestDeviceInfo(deviceId);
            var deviceInfo2 = CreateTestDeviceInfo(deviceId);

            // Act
            var hashCode1 = deviceInfo1.GetHashCode();
            var hashCode2 = deviceInfo2.GetHashCode();

            // Assert
            hashCode1.Should().Be(hashCode2);
        }

        [Fact]
        public void GetHashCode_DifferentDeviceId_DifferentHashCode()
        {
            // Arrange
            var deviceInfo1 = CreateTestDeviceInfo(Guid.NewGuid());
            var deviceInfo2 = CreateTestDeviceInfo(Guid.NewGuid());

            // Act
            var hashCode1 = deviceInfo1.GetHashCode();
            var hashCode2 = deviceInfo2.GetHashCode();

            // Assert
            hashCode1.Should().NotBe(hashCode2);
        }

        [Fact]
        public void ToString_ReturnsDeviceName()
        {
            // Arrange
            var deviceName = "My Test Device";
            var deviceInfo = CreateTestDeviceInfo(deviceName: deviceName);

            // Act
            var stringRepresentation = deviceInfo.ToString();

            // Assert
            stringRepresentation.Should().Be(deviceName);
        }

        [Fact]
        public void ConnectionDuration_CalculatesCorrectly()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            var connectedAt = DateTime.UtcNow.AddMinutes(-5);
            var deviceInfoType = typeof(DeviceInfo);
            var connectedAtField = deviceInfoType.GetField("_connectedAt", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            connectedAtField.SetValue(deviceInfo, connectedAt);

            // Act
            var duration = deviceInfo.ConnectionDuration;

            // Assert
            duration.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void TimeSinceLastSeen_CalculatesCorrectly()
        {
            // Arrange
            var deviceInfo = CreateTestDeviceInfo();
            var lastSeenAt = DateTime.UtcNow.AddMinutes(-2);
            var deviceInfoType = typeof(DeviceInfo);
            var lastSeenField = deviceInfoType.GetField("_lastSeenAt", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            lastSeenField.SetValue(deviceInfo, lastSeenAt);

            // Act
            var timeSinceLastSeen = deviceInfo.TimeSinceLastSeen;

            // Assert
            timeSinceLastSeen.Should().BeCloseTo(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            // Arrange
            var original = CreateTestDeviceInfo();
            original.UpdateFeatures(SessionFeatures.Keyboard | SessionFeatures.Mouse);
            original.UpdateLastSeen();
            
            // Wait to ensure different timestamps
            System.Threading.Thread.Sleep(10);

            // Act
            var clone = original.Clone();

            // Assert
            clone.Should().NotBeSameAs(original);
            clone.DeviceId.Should().Be(original.DeviceId);
            clone.DeviceName.Should().Be(original.DeviceName);
            clone.Platform.Should().Be(original.Platform);
            clone.Role.Should().Be(original.Role);
            clone.Fingerprint.Should().Be(original.Fingerprint);
            clone.EnabledFeatures.Should().Be(original.EnabledFeatures);
            
            // Timestamps should be preserved
            clone.ConnectedAt.Should().Be(original.ConnectedAt);
            clone.LastSeenAt.Should().Be(original.LastSeenAt);
        }

        [Fact]
        public void WithRole_CreatesNewInstanceWithUpdatedRole()
        {
            // Arrange
            var original = CreateTestDeviceInfo();
            var newRole = DeviceRole.Client;

            // Act
            var updated = original.WithRole(newRole);

            // Assert
            updated.Should().NotBeSameAs(original);
            updated.DeviceId.Should().Be(original.DeviceId);
            updated.DeviceName.Should().Be(original.DeviceName);
            updated.Platform.Should().Be(original.Platform);
            updated.Role.Should().Be(newRole);
            updated.Fingerprint.Should().Be(original.Fingerprint);
            updated.EnabledFeatures.Should().Be(original.EnabledFeatures);
        }

        [Fact]
        public void WithFeatures_CreatesNewInstanceWithUpdatedFeatures()
        {
            // Arrange
            var original = CreateTestDeviceInfo();
            var newFeatures = SessionFeatures.Screen | SessionFeatures.Clipboard;

            // Act
            var updated = original.WithFeatures(newFeatures);

            // Assert
            updated.Should().NotBeSameAs(original);
            updated.DeviceId.Should().Be(original.DeviceId);
            updated.DeviceName.Should().Be(original.DeviceName);
            updated.Platform.Should().Be(original.Platform);
            updated.Role.Should().Be(original.Role);
            updated.Fingerprint.Should().Be(original.Fingerprint);
            updated.EnabledFeatures.Should().Be(newFeatures);
        }

        private DeviceInfo CreateTestDeviceInfo(
            Guid? deviceId = null,
            string deviceName = null,
            string platform = null,
            DeviceRole? role = null,
            DeviceFingerprint fingerprint = null)
        {
            deviceId ??= Guid.NewGuid();
            deviceName ??= "Test Device";
            platform ??= "Test Platform";
            role ??= DeviceRole.Server;
            fingerprint ??= new DeviceFingerprint(new string('A', 64));

            return new DeviceInfo(deviceId.Value, deviceName, platform, role.Value, fingerprint);
        }
    }
}