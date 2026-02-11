using System;
using CrossInputShare.Core.Models;

namespace CrossInputShare.LinuxConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Cross-Platform Input Sharing - Linux Console Demo ===");
            Console.WriteLine();
            
            // Test 1: Session Code Generation
            Console.WriteLine("Test 1: Session Code Generation");
            try
            {
                var sessionCode = SessionCode.Generate();
                Console.WriteLine($"Generated session code: {sessionCode}");
                Console.WriteLine($"Display format: {sessionCode.ToDisplayString()}");
                Console.WriteLine($"Random part: {sessionCode.RandomPart}");
                Console.WriteLine($"Checksum: {sessionCode.Checksum}");
                Console.WriteLine($"Is valid: {SessionCode.IsValid(sessionCode.ToString())}");
                Console.WriteLine("✓ Session code generation works!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Session code generation failed: {ex.Message}");
            }
            
            Console.WriteLine();
            
            // Test 2: Device Fingerprint
            Console.WriteLine("Test 2: Device Fingerprint");
            try
            {
                var fingerprint = DeviceFingerprint.Generate(
                    platformInfo: "Linux 6.14.0-37-generic x64",
                    machineId: "test-machine-001",
                    installationId: "test-install-001"
                );
                Console.WriteLine($"Generated fingerprint: {fingerprint}");
                Console.WriteLine($"Short display: {fingerprint.ShortDisplay}");
                Console.WriteLine($"Medium display: {fingerprint.MediumDisplay}");
                Console.WriteLine("✓ Device fingerprint generation works!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Device fingerprint generation failed: {ex.Message}");
            }
            
            Console.WriteLine();
            
            // Test 3: Device Info
            Console.WriteLine("Test 3: Device Info");
            try
            {
                var fingerprint = DeviceFingerprint.Generate(
                    platformInfo: "Linux 6.14.0-37-generic x64",
                    machineId: "test-machine-001",
                    installationId: "test-install-001"
                );
                var deviceInfo = DeviceInfo.CreateLocal(fingerprint, "Linux Test Device", "Ubuntu 22.04", isHost: true);
                Console.WriteLine($"Device ID: {deviceInfo.Id}");
                Console.WriteLine($"Device name: {deviceInfo.Name}");
                Console.WriteLine($"Platform: {deviceInfo.Platform}");
                Console.WriteLine($"Is host: {deviceInfo.IsHost}");
                Console.WriteLine($"Joined at: {deviceInfo.JoinedAt}");
                Console.WriteLine($"Fingerprint: {deviceInfo.Fingerprint.ShortDisplay}");
                Console.WriteLine("✓ Device info creation works!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Device info creation failed: {ex.Message}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== Demo Complete ===");
            Console.WriteLine("Demo completed successfully!");
        }
    }
}