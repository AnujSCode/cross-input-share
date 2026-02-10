# Cross-Platform Input Sharing Software - Design Document

## 1. System Architecture Overview

### 1.1 Core Components

The system consists of three main components:

1. **Client Applications** (per platform):
   - Ubuntu Desktop Client (native)
   - Windows Desktop Client (native)  
   - Android Mobile Client (native)
   
2. **Signaling & Relay Server** (cloud-based):
   - Manages session establishment
   - Provides NAT traversal assistance
   - Optional relay for direct-connect failures
   - Handles authentication and session management

3. **Local Discovery Service** (optional):
   - For LAN discovery when devices are on same network
   - Uses multicast DNS (mDNS) for zero-configuration

### 1.2 Architectural Pattern: Hybrid P2P with Central Coordination

```
┌─────────┐      ┌──────────────┐      ┌─────────┐
│  Client │──────│   Signaling  │──────│  Client │
│   A     │      │    Server    │      │   B     │
└─────────┘      └──────────────┘      └─────────┘
      │                  │                  │
      └──────────────────┼──────────────────┘
                         │
                (Session Negotiation)
                         │
      ┌──────────────────┼──────────────────┐
      │                  │                  │
┌─────────┐      ┌──────────────┐      ┌─────────┐
│  Client │══════│   Direct     │══════│  Client │
│   A     │      │   P2P        │      │   B     │
└─────────┘      │ Connection   │      └─────────┘
                 └──────────────┘
          (Encrypted Data Channel)
```

### 1.3 Platform-Specific Implementation

**Ubuntu/Windows Desktop Clients:**
- Native applications using system APIs for input capture/injection
- Low-level input hooks (X11 on Linux, WinAPI on Windows)
- Clipboard integration via system APIs
- Written in Rust/C++ for performance with platform-specific backends

**Android Client:**
- Uses Accessibility Service for input capture
- Requires appropriate permissions
- Foreground service for reliability
- Kotlin/Java implementation

## 2. Network Protocol Design

### 2.1 Signaling Protocol (WebSocket/HTTP)

**Purpose:** Establish peer connections, exchange session codes, negotiate capabilities.

**Message Types:**
1. `SESSION_CREATE` - Create new session with parameters
2. `SESSION_JOIN` - Join existing session with code
3. `OFFER`/`ANSWER` - WebRTC SDP exchange
4. `ICE_CANDIDATE` - ICE candidate exchange
5. `SESSION_CLOSE` - Terminate session
6. `HEARTBEAT` - Keepalive

**Flow:**
1. Client A creates session → Server generates 6-digit code + fingerprint
2. Client B enters code → Server validates + establishes signaling channel
3. WebRTC negotiation through server relay
4. Direct P2P connection established (or relayed if necessary)

### 2.2 Data Protocol (Custom over WebRTC DataChannel)

**Encapsulation Format:**
```
┌─────────────────────────────────────────┐
│ Header (4 bytes)                        │
│   - Message Type (1 byte)               │
│   - Sequence Number (2 bytes)           │
│   - Flags (1 byte)                      │
├─────────────────────────────────────────┤
│ Payload (variable length)               │
│   - Encrypted with session key          │
└─────────────────────────────────────────┘
```

**Message Types:**
- `INPUT_EVENT` - Keyboard/mouse events
- `CLIPBOARD_UPDATE` - Clipboard content
- `SCREEN_FRAME` - Screen sharing data (optional)
- `KEEPALIVE` - Connection health check
- `CONFIRMATION_REQUEST` - Sensitive action confirmation
- `AUDIT_LOG` - Security audit events

### 2.3 Transport Layer

**Primary:** WebRTC DataChannels
- Built-in encryption (DTLS-SRTP)
- NAT traversal (STUN/ICE)
- Congestion control
- Reliable and unreliable channels

**Fallback:** TURN relay (via signaling server)
- Used when direct P2P fails
- Adds latency but ensures connectivity

## 3. Security Model Specification

### 3.1 End-to-End Encryption

**Key Exchange:** Elliptic Curve Diffie-Hellman (ECDH) over Curve25519
- Session-specific keys generated during connection establishment
- Perfect forward secrecy (new keys each session)

**Encryption:** AES-256-GCM for data channels
- Authenticated encryption with associated data (AEAD)
- Prevents tampering and ensures confidentiality

**Key Derivation:** HKDF-SHA256
- Derives encryption and authentication keys from shared secret

### 3.2 Authentication & Session Security

**Session Codes:**
- 6-digit alphanumeric (36^6 ≈ 2.1B combinations)
- Time-limited: 10-minute validity
- One-time use: invalidated after successful connection
- Generated server-side with cryptographically secure RNG

**Device Fingerprinting:**
- Combines multiple factors:
  - Device ID (platform-specific)
  - Public key fingerprint
  - Hardware characteristics (non-PII)
  - Installation-specific salt
- Fingerprint shown to users for verification

**Manual Verification:**
- Users must compare/confirm fingerprints
- Prevents man-in-the-middle attacks
- Required before any data transfer

### 3.3 Access Control & Rate Limiting

**Connection Throttling:**
- Maximum 5 concurrent devices per session
- Rate limits per connection:
  - Input events: 1000/sec max
  - Clipboard updates: 10/sec max  
  - Screen frames: 30/sec max (if enabled)
- Automatic backoff on violations

**Geographic Restrictions (Optional):**
- Country-level blocking configurable
- IP reputation checks

### 3.4 User Confirmation Prompts

**Trigger Events:**
1. First connection from new device
2. Clipboard access (read/write)
3. File transfer initiation
4. Screen sharing request
5. Administrative actions

**Implementation:**
- Modal dialog requiring explicit user approval
- Timeout with automatic denial (30 seconds)
- Audit log entry for all prompts

## 4. Session Management Design

### 4.1 Session Lifecycle

```
CREATE → PENDING → ACTIVE → CLOSING → TERMINATED
     ↑          ↓         ↓
     └──EXPIRED─┘         └──ERROR
```

**States:**
- `PENDING`: Code generated, waiting for peers
- `ACTIVE`: At least 2 devices connected
- `CLOSING`: Graceful termination in progress
- `TERMINATED`: Session ended, resources freed
- `EXPIRED`: Code timeout (10 minutes)

### 4.2 Reconnection Policy

**Strict Manual Reconnection:**
- No automatic reconnections after disconnection
- Users must enter code again after any restart
- Session codes invalidated on first use
- New session required for reconnection

**Exception:** Temporary network issues within same session maintain connection via WebRTC ICE

### 4.3 Device Management

**Device Registry:**
- Each device has unique fingerprint
- Devices can be named by user
- Trust relationships can be established (optional future feature)

**Session Roles:**
- `HOST`: Device that created session (can invite others)
- `PEER`: Joined devices (can be promoted to host)
- `VIEWER`: Read-only access (screen sharing only)

## 5. Performance Optimization Strategies

### 5.1 Latency Reduction Techniques

**Input Event Compression:**
- Delta encoding for mouse movements
- Key state bitmask for keyboard
- Batch small events when possible

**Network Optimization:**
- UDP-based WebRTC for lower latency
- QoS tagging for input events (higher priority)
- Adaptive bitrate for screen sharing
- Local network discovery bypasses relay

**Platform-Specific Optimizations:**

**Linux (Ubuntu):**
- Raw input device access via evdev
- Kernel bypass for lower latency
- Shared memory for clipboard

**Windows:**
- Raw Input API for direct HID access  
- Low-level hooks with minimal overhead
- DirectX capture for screen sharing

**Android:**
- Accessibility service with event filtering
- Hardware-accelerated encoding
- Foreground service priority

### 5.2 Resource Management

**Memory Efficiency:**
- Zero-copy architectures where possible
- Object pooling for frequent allocations
- Streaming compression for screen data

**CPU Optimization:**
- Event batching to reduce context switches
- Hardware acceleration for encryption/compression
- Background processing at lower priority

**Battery Considerations (Mobile):**
- Adaptive polling based on activity
- Screen-off throttling
- Background service optimizations

### 5.3 Scalability Considerations

**Server-Side:**
- Stateless design for horizontal scaling
- Connection pooling for database
- Cached session information

**Client-Side:**
- Graceful degradation with many devices
- Efficient broadcast to multiple peers
- Load balancing across CPU cores

## 6. Technical Specifications

### 6.1 Platform Requirements

**Ubuntu:**
- Version: 20.04 LTS or newer
- Architecture: x86_64, ARM64
- Dependencies: libx11, libxtst, libevdev
- Permissions: Input device access, network

**Windows:**
- Version: Windows 10 or newer
- Architecture: x86_64, ARM64
- Dependencies: .NET Framework 4.8 (or equivalent)
- Permissions: Administrator for input hooks

**Android:**
- Version: Android 8.0 (API 26) or newer
- Permissions: Accessibility service, foreground service, network

### 6.2 Protocol Specifications

**Signaling Server API:**
- Base URL: `https://signal.inputshare.example.com`
- WebSocket endpoint: `/ws`
- REST endpoints for session management
- Rate limiting headers

**Data Channel Protocol:**
- Maximum message size: 16KB
- Compression: LZ4 for screen data
- Heartbeat interval: 30 seconds
- Timeout: 90 seconds no heartbeat

### 6.3 Security Specifications

**Cryptographic Algorithms:**
- Key exchange: X25519 (ECDH)
- Encryption: AES-256-GCM
- Hash: SHA-256
- Signature: Ed25519 (for future authentication)

**Certificate Pinning:**
- Hardcoded server certificates in clients
- Certificate transparency logs
- Regular key rotation

**Audit Requirements:**
- All connections logged (timestamp, device ID, IP)
- Security events encrypted and stored
- Retention: 90 days minimum

## 7. Additional Security Features for Phase 3 Review

### 7.1 Advanced Threat Protection

**Behavioral Analysis:**
- Anomaly detection for input patterns
- Geographic velocity checks
- Device fingerprint drift detection

**Intrusion Prevention:**
- Automatic blocking of suspicious connections
- Integration with threat intelligence feeds
- Honey token sessions for detection

### 7.2 Privacy Enhancements

**Data Minimization:**
- No PII collection
- Ephemeral session data
- Client-side processing where possible

**Transparency:**
- Open-source client code
- External security audits
- Public bug bounty program

### 7.3 Compliance Features

**Regulatory Compliance:**
- GDPR compliance tools
- Data residency options
- Export controls

**Enterprise Features:**
- LDAP/Active Directory integration
- SIEM integration for audit logs
- Policy-based access controls

### 7.4 Resilience Features

**Disaster Recovery:**
- Multi-region server deployment
- Client-side session caching
- Offline mode for LAN operation

**Anti-Censorship:**
- Domain fronting capabilities
- Pluggable transports
- Decentralized signaling options

## 8. Implementation Roadmap

### Phase 1: Core Functionality (MVP)
- Basic input sharing between 2 devices
- Manual session codes
- End-to-end encryption
- Ubuntu/Windows clients

### Phase 2: Enhanced Features
- Android client
- Screen sharing
- Clipboard synchronization
- Up to 5 device support

### Phase 3: Security Hardening
- All security features from Section 7
- Advanced threat protection
- Enterprise features

### Phase 4: Optimization & Scaling
- Performance optimizations
- Additional platform support
- Cloud scaling improvements

## 9. Risk Mitigation

### Technical Risks:
- **Input latency**: Mitigated by local optimization and protocol design
- **Platform compatibility**: Mitigated by extensive testing matrix
- **Security vulnerabilities**: Mitigated by external audits and bug bounty

### Operational Risks:
- **Server costs**: Freemium model with paid tiers for heavy usage
- **User adoption**: Simple UX with progressive disclosure of features
- **Regulatory changes**: Modular design for compliance adaptations

## 10. Conclusion

This design provides a robust foundation for cross-platform input sharing with security as a primary consideration. The hybrid P2P architecture balances performance with connectivity, while the comprehensive security model addresses both technical and user-focused threats.

The implementation can proceed incrementally, with each phase delivering increasing value while maintaining backward compatibility and security posture.