# Cross-Platform Input Sharing Software

A secure, cross-platform solution for sharing keyboard, mouse, clipboard, and screen input between multiple devices.

## Features

- **Cross-Platform Support**: Windows, Ubuntu, Android (planned)
- **Secure Communication**: End-to-end encryption with X25519 key exchange
- **Multiple Input Types**: Keyboard, mouse, clipboard, and screen sharing
- **Flexible Topology**: Star topology with server-client model
- **Session Management**: Secure session codes with expiration
- **Manual Verification**: Device fingerprint comparison for security

## Architecture

The application follows a clean architecture with separation of concerns:

```
CrossInputShare/
├── Core/           # Core models and interfaces
├── Security/       # Encryption and key exchange services
├── Network/        # Connection routing and WebRTC implementation
├── Platform/       # Platform-specific input capture
├── UI/            # WinUI 3 user interface
└── Tests/         # Unit and integration tests
```

## Prerequisites

### Windows
- Windows 10/11 (version 1809 or later)
- .NET 8 SDK
- Visual Studio 2022 (optional, for development)

### Ubuntu
- Ubuntu 22.04 or later
- .NET 8 SDK
- X11 development libraries

### Android (Planned)
- Android 8.0 (API 26) or later
- Xamarin/MAUI development environment

## Quick Start

### 1. Clone the Repository
```bash
git clone <repository-url>
cd cross_input_share
```

### 2. Build the Solution
```bash
dotnet build src/CrossInputShare.sln
```

### 3. Run the Application (Windows)
```bash
cd src/CrossInputShare.UI
dotnet run
```

## Building from Source

### Windows
```bash
# Restore dependencies
dotnet restore src/CrossInputShare.sln

# Build in Release mode
dotnet build src/CrossInputShare.sln -c Release

# Run tests
dotnet test src/CrossInputShare.sln
```

### Ubuntu
```bash
# Install .NET 8 SDK
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Build the solution
dotnet build src/CrossInputShare.sln
```

## Development

### Project Structure

- **CrossInputShare.Core**: Core domain models and interfaces
- **CrossInputShare.Security**: Cryptographic services (encryption, key exchange)
- **CrossInputShare.Network**: Network communication and WebRTC implementation
- **CrossInputShare.Platform**: Platform-specific input capture implementations
- **CrossInputShare.UI**: WinUI 3 user interface application
- **CrossInputShare.Tests**: Unit and integration tests

### Code Style

This project uses EditorConfig for consistent code style. Key conventions:

- **Indentation**: 4 spaces
- **Naming**: PascalCase for types/methods, _camelCase for private fields
- **Accessibility**: Explicit access modifiers required
- **XML Documentation**: Required for public APIs

Run code analysis:
```bash
dotnet format --verify-no-changes
```

### Testing

Run all tests:
```bash
dotnet test src/CrossInputShare.sln
```

Run specific test projects:
```bash
dotnet test src/CrossInputShare.Tests
```

## Security

### Key Features
- **X25519 Key Exchange**: Modern elliptic curve Diffie-Hellman for secure key establishment
- **AES-GCM Encryption**: Authenticated encryption for confidentiality and integrity
- **Session Codes**: Cryptographically secure 9-character codes with checksum
- **Device Fingerprinting**: SHA-256 fingerprints for manual verification
- **Forward Secrecy**: Key rotation support

### Security Considerations
1. **Manual Verification**: Always verify device fingerprints before sharing input
2. **Session Expiration**: Sessions automatically expire after 10 minutes of inactivity
3. **Rate Limiting**: Connection attempts are rate-limited to prevent brute-force attacks
4. **Secure Disposal**: Cryptographic keys are zeroized from memory when no longer needed

## Network Protocol

### Connection Establishment
1. **Signaling**: WebRTC signaling server facilitates peer-to-peer connections
2. **Key Exchange**: X25519 key exchange establishes shared secrets
3. **Verification**: Users manually compare device fingerprints
4. **Session Setup**: Input sharing features are negotiated

### Data Channels
- **Control Channel**: Encrypted JSON messages for session management
- **Input Channel**: Binary data for keyboard/mouse events
- **Clipboard Channel**: Text and file transfer
- **Screen Channel**: Compressed video frames for screen sharing

## Platform Support

### Windows
- **Input Capture**: Windows API hooks for keyboard/mouse events
- **Screen Capture**: DirectX/DXGI for efficient screen capture
- **Clipboard**: Windows clipboard API with format translation

### Ubuntu
- **Input Capture**: X11/XInput for keyboard/mouse events
- **Screen Capture**: X11/XShm for screen capture
- **Clipboard**: X11 clipboard with ICCCM protocol

### Android (Planned)
- **Input Capture**: Accessibility Service for input events
- **Screen Capture**: MediaProjection API for screen recording
- **Clipboard**: Android clipboard manager

## Configuration

### Application Settings
Configuration is stored in `appsettings.json`:

```json
{
  "Network": {
    "SignalingServer": "wss://signaling.example.com",
    "StunServers": ["stun:stun.l.google.com:19302"],
    "TurnServers": []
  },
  "Security": {
    "SessionTimeoutMinutes": 10,
    "MaxConnectionAttempts": 5,
    "KeyRotationIntervalHours": 24
  },
  "Features": {
    "DefaultEnabled": ["Keyboard", "Mouse"],
    "RequireVerification": true
  }
}
```

### Platform-Specific Settings
- **Windows**: Registry settings for auto-start and permissions
- **Ubuntu**: Systemd service configuration
- **Android**: AndroidManifest permissions and service declarations

## Troubleshooting

### Common Issues

#### Connection Failed
1. Check firewall settings (ports 3478, 5349 for STUN/TURN)
2. Verify signaling server is accessible
3. Check network connectivity between devices

#### Input Not Working
1. Verify device permissions (admin/root may be required)
2. Check if input features are enabled in session
3. Verify device verification is complete

#### Screen Sharing Issues
1. Check display permissions (screen recording access)
2. Verify sufficient bandwidth for screen sharing
3. Check graphics driver compatibility

### Logging
Enable verbose logging by setting environment variable:
```bash
export LOG_LEVEL=Debug
```

Logs are stored in:
- **Windows**: `%LOCALAPPDATA%\CrossInputShare\logs\`
- **Ubuntu**: `~/.local/share/CrossInputShare/logs/`
- **Android**: App-specific logs directory

## Contributing

### Development Workflow
1. Fork the repository
2. Create a feature branch
3. Make changes with tests
4. Ensure code style compliance
5. Submit a pull request

### Code Review Checklist
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] Code follows style guidelines
- [ ] Security considerations addressed
- [ ] Cross-platform compatibility verified

### Building Documentation
```bash
# Generate API documentation
dotnet build src/CrossInputShare.sln -t:DocFx

# Serve documentation locally
docfx serve docs/_site
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- **WebRTC**: Peer-to-peer communication protocol
- **libsodium**: Modern cryptography library
- **WinUI 3**: Modern Windows UI framework
- **.NET 8**: Cross-platform development framework

## Support

For issues and questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Search existing GitHub issues
3. Create a new issue with detailed information

---

**Security Notice**: This software handles sensitive input data. Always verify device fingerprints and use in trusted networks only.