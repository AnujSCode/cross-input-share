# System Architecture Overview

## Introduction

Cross-Platform Input Sharing Software is a secure, cross-platform solution for sharing keyboard, mouse, clipboard, and screen input between multiple devices. The system enables users to control multiple computers from a single input device or share input across devices in a collaborative session.

## Architecture Principles

### 1. Security-First Design
- End-to-end encryption for all data transmission
- Manual device verification through fingerprint comparison
- Least privilege principle for feature access
- Secure key exchange and rotation

### 2. Cross-Platform Compatibility
- Abstract platform-specific implementations
- Consistent API across all platforms
- Graceful degradation for platform limitations
- Unified configuration management

### 3. Performance & Responsiveness
- Low-latency input transmission
- Efficient screen encoding algorithms
- Memory-conscious design
- Async/await patterns for UI responsiveness

### 4. Reliability & Resilience
- Automatic reconnection with exponential backoff
- Graceful error handling and recovery
- Session persistence and state management
- Comprehensive logging and monitoring

## System Architecture

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    User Interface Layer                      │
├─────────────────────────────────────────────────────────────┤
│  CrossInputShare.UI (WinUI 3)                               │
│  • Main Window & Controls                                   │
│  • ViewModels (MVVM Pattern)                                │
│  • User Configuration                                       │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                    Business Logic Layer                      │
├─────────────────────────────────────────────────────────────┤
│  CrossInputShare.Core                                       │
│  • Domain Models                                            │
│  • Interfaces & Contracts                                   │
│  • Session Management                                       │
│  • Feature Configuration                                    │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                    Service Layer                             │
├───────────────┬───────────────┬───────────────┬─────────────┤
│ Security      │ Network       │ Platform      │ Tests       │
├───────────────┼───────────────┼───────────────┼─────────────┤
│ • Encryption  │ • WebRTC      │ • Windows     │ • Unit      │
│ • Key Exchange│ • Signaling   │ • Ubuntu      │ • Integration│
│ • Auth        │ • Routing     │ • Android     │ • Security  │
│ • Validation  │ • Protocols   │ • Input Capture│ • Perf     │
└───────────────┴───────────────┴───────────────┴─────────────┘
```

## Core Components

### 1. CrossInputShare.Core
The central domain layer containing all business logic and models.

#### Key Models:
- **DeviceInfo**: Represents a connected device with identity, role, and features
- **DeviceFingerprint**: Cryptographic fingerprint for device verification
- **SessionCode**: Secure session codes for authentication
- **SessionInfo**: Manages session state and participant devices
- **SessionFeatures**: Feature flags for input sharing capabilities
- **InputEvents**: Platform-agnostic input event representations

#### Key Interfaces:
- **IConnectionRouter**: Manages input routing between devices
- **IDeviceConnection**: Abstract connection to a remote device
- **IEncryptionService**: Cryptographic operations interface
- **ISessionManager**: Session lifecycle management

### 2. CrossInputShare.Security
Cryptographic services and security implementations.

#### Services:
- **AesGcmEncryptionService**: AES-GCM authenticated encryption
- **ChaCha20Poly1305EncryptionService**: Alternative encryption for platforms without AES-NI
- **KeyExchangeService**: X25519 key exchange with HKDF key derivation

#### Security Features:
- End-to-end encryption for all transmitted data
- Forward secrecy through key rotation
- Device fingerprint verification
- Session code validation and expiration

### 3. CrossInputShare.Network
Network communication and WebRTC implementation.

#### Components:
- **ConnectionRouter**: Implements star topology routing
- **WebRTC Signaling**: Session establishment and NAT traversal
- **Data Channels**: Separate channels for different data types
- **Connection Pooling**: Efficient connection reuse

#### Protocols:
- WebRTC for peer-to-peer communication
- Secure WebSocket for signaling
- Custom binary protocol for input events
- JSON for control messages

### 4. CrossInputShare.Platform
Platform-specific implementations.

#### Windows:
- WinAPI hooks for input capture
- DirectX/DXGI for screen capture
- Windows clipboard API integration

#### Ubuntu:
- X11/XInput for input capture
- X11/XShm for screen capture
- X11 clipboard integration

#### Android (Planned):
- Accessibility Service for input capture
- MediaProjection API for screen capture
- Android clipboard integration

### 5. CrossInputShare.UI
WinUI 3 user interface application.

#### Architecture:
- MVVM pattern with CommunityToolkit.Mvvm
- Reactive UI with data binding
- Modern WinUI 3 controls and styling
- Responsive design for different window sizes

#### Key Screens:
- Main session management
- Device connection and verification
- Feature configuration
- Session history and statistics

## Data Flow

### 1. Session Establishment
```
1. User creates/joins session → Generates/validates session code
2. Devices exchange public keys → X25519 key exchange
3. Users verify fingerprints → Manual security check
4. Session features negotiated → Based on user preferences
5. WebRTC connections established → Peer-to-peer network
```

### 2. Input Sharing Flow
```
1. Input captured on source device → Platform-specific capture
2. Events serialized → Binary format for efficiency
3. Data encrypted → AES-GCM with session key
4. Transmitted via WebRTC → Low-latency data channel
5. Received and decrypted → Verification of authentication tag
6. Events replayed on target → Platform-specific injection
```

### 3. Screen Sharing Flow
```
1. Screen captured → Platform-specific capture (DXGI/X11)
2. Frame encoded → H.264/VP8 for efficiency
3. Encrypted and transmitted → Secure video stream
4. Received and decoded → Hardware acceleration when available
5. Displayed on target → Real-time video rendering
```

## Security Architecture

### 1. Authentication & Authorization
- **Session Codes**: Cryptographically secure 9-character codes
- **Device Fingerprints**: SHA-256 hashes for manual verification
- **Role-Based Access**: Server vs. Client permissions
- **Feature Gates**: Per-feature authorization checks

### 2. Cryptography
- **Key Exchange**: X25519 elliptic curve Diffie-Hellman
- **Encryption**: AES-256-GCM or ChaCha20-Poly1305
- **Key Derivation**: HKDF for key material expansion
- **Forward Secrecy**: Regular key rotation

### 3. Network Security
- **WebRTC Security**: DTLS-SRTP for media encryption
- **Signaling Security**: TLS 1.3 for WebSocket connections
- **Certificate Pinning**: Prevention of MITM attacks
- **Rate Limiting**: Protection against brute-force attacks

## Deployment Architecture

### Windows Deployment
```
┌─────────────────────────────────────────────────────────────┐
│                    Windows Device                            │
├─────────────────────────────────────────────────────────────┤
│  CrossInputShare.UI.exe                                     │
│  • WinUI 3 Desktop Application                              │
│  • Runs with user privileges                                │
│  • Auto-start option                                        │
└─────────────────────────────────────────────────────────────┘
```

### Ubuntu Deployment
```
┌─────────────────────────────────────────────────────────────┐
│                    Ubuntu Device                             │
├─────────────────────────────────────────────────────────────┤
│  cross-input-share (.NET binary)                            │
│  • .NET 8 Self-Contained Deployment                         │
│  • Systemd service for auto-start                           │
│  • X11 session integration                                  │
└─────────────────────────────────────────────────────────────┘
```

### Android Deployment (Planned)
```
┌─────────────────────────────────────────────────────────────┐
│                    Android Device                            │
├─────────────────────────────────────────────────────────────┤
│  CrossInputShare.apk                                        │
│  • Xamarin/MAUI Application                                 │
│  • Foreground service for background operation              │
│  • Accessibility service permission                         │
└─────────────────────────────────────────────────────────────┘
```

## Scalability Considerations

### 1. Session Size
- **Small Sessions**: 2-5 devices (optimized for latency)
- **Medium Sessions**: 6-15 devices (hierarchical routing)
- **Large Sessions**: 16+ devices (requires server relay)

### 2. Network Topology
- **Star Topology**: Default for small sessions
- **Mesh Topology**: Optional for reduced server load
- **Hybrid Approach**: Adaptive based on network conditions

### 3. Resource Management
- **Connection Pooling**: Reuse WebRTC connections
- **Memory Pools**: Reduce GC pressure for high-frequency events
- **Bandwidth Adaptation**: Adjust quality based on available bandwidth

## Monitoring & Observability

### 1. Logging
- Structured logging with Serilog
- Different log levels for development/production
- Secure logging (no sensitive data in logs)

### 2. Metrics
- Input latency measurements
- Network bandwidth usage
- Memory and CPU utilization
- Error rates and connection statistics

### 3. Health Checks
- Connection health monitoring
- Resource usage alerts
- Automatic recovery procedures

## Future Extensions

### 1. Planned Features
- **Cloud Relay**: For NAT traversal in restrictive networks
- **Session Recording**: Encrypted session recording and playback
- **Advanced Input**: Gamepad, touch, and stylus support
- **Enterprise Features**: Active Directory integration, audit logging

### 2. Platform Expansion
- **macOS Support**: Native macOS implementation
- **iOS Support**: Limited input sharing capabilities
- **Web Client**: Browser-based client using WebRTC
- **Linux Variants**: Support for other Linux distributions

### 3. Integration Points
- **Password Managers**: Secure credential sharing
- **Remote Support**: Integration with helpdesk systems
- **Automation Tools**: Scriptable input sequences
- **Accessibility**: Enhanced accessibility features

## Conclusion

The Cross-Platform Input Sharing Software architecture is designed with security, performance, and cross-platform compatibility as primary concerns. The modular design allows for easy extension to new platforms and features while maintaining a consistent user experience across all supported devices.