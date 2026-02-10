# Deliverable 1: System Architecture Diagram & Description

## High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CLOUD INFRASTRUCTURE                        │
│                                                                     │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐         │
│  │   Signaling  │    │    STUN      │    │    TURN      │         │
│  │    Server    │    │    Server    │    │    Server    │         │
│  │   (WebSocket)│    │  (NAT Traversal) │   (Relay)     │         │
│  └──────┬───────┘    └──────────────┘    └──────────────┘         │
│         │                                                            │
└─────────┼────────────────────────────────────────────────────────────┘
          │
          │                       Internet
          │
┌─────────┼────────────────────────────────────────────────────────────┐
│                         CLIENT DEVICES                               │
│                                                                     │
│  ┌──────────────┐                    ┌──────────────┐              │
│  │   Ubuntu     │                    │   Windows    │              │
│  │   Client     │◄──────────────────►│   Client     │              │
│  │              │    P2P Connection  │              │              │
│  └──────────────┘                    └──────────────┘              │
│         │                                    │                      │
│         ▼                                    ▼                      │
│  ┌──────────────┐                    ┌──────────────┐              │
│  │  Input       │                    │  Input       │              │
│  │  Capture     │                    │  Capture     │              │
│  │  (X11/evdev) │                    │  (WinAPI)    │              │
│  └──────────────┘                    └──────────────┘              │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                       Android Client                         │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │   │
│  │  │ Accessibility│  │  Input       │  │   Network    │      │   │
│  │  │   Service    │  │  Injection   │  │   Stack      │      │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘      │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

## Component Descriptions

### 1. Signaling Server
**Purpose:** Coordinates connection establishment between devices
**Protocol:** WebSocket with JSON messages
**Functions:**
- Session creation and code generation
- Device fingerprint verification
- WebRTC offer/answer exchange
- Session state management
- Rate limiting and audit logging

**Scalability:** Stateless design, can be horizontally scaled

### 2. STUN Server
**Purpose:** NAT traversal for direct P2P connections
**Protocol:** STUN (Session Traversal Utilities for NAT)
**Functions:**
- Discovers public IP:port mappings
- Helps establish direct connections
- Lightweight, no data relay

### 3. TURN Server
**Purpose:** Relay server when direct P2P fails
**Protocol:** TURN (Traversal Using Relays around NAT)
**Functions:**
- Relays encrypted data between peers
- Used only as fallback (adds latency)
- Bandwidth metering and limiting

### 4. Client Architecture (All Platforms)

```
┌─────────────────────────────────────────────────┐
│                  User Interface                  │
│  ┌─────────────┐ ┌─────────────┐ ┌───────────┐ │
│  │ Session     │ │ Device      │ │ Settings  │ │
│  │ Management  │ │ List        │ │ & Security│ │
│  └─────────────┘ └─────────────┘ └───────────┘ │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│                Connection Manager                │
│  • Signaling client                             │
│  • WebRTC negotiation                           │
│  • NAT traversal                                │
│  • Reconnection logic                           │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│                 Security Layer                   │
│  • Encryption/decryption                        │
│  • Key management                               │
│  • Device fingerprinting                        │
│  • Audit logging                                │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│               Data Processing Layer              │
│  • Input event serialization                    │
│  • Clipboard synchronization                    │
│  • Screen capture/encoding (optional)           │
│  • Compression                                  │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│              Platform Abstraction Layer          │
│  • Input capture/injection                      │
│  • Clipboard access                             │
│  • System integration                           │
│  • Power management                             │
└─────────────────────────────────────────────────┘
```

## Data Flow

### Connection Establishment Flow

```
1. User A creates session:
   Client → Signaling Server: CREATE_SESSION
   Server → Client: Session code (6-digit), fingerprint
   
2. User B joins session:
   Client → Server: JOIN_SESSION(code)
   Server → Both: Exchange fingerprints for verification
   
3. User verification:
   Both users compare fingerprints manually
   Users confirm via UI
   
4. WebRTC negotiation:
   Client A → Server: OFFER (SDP)
   Server → Client B: OFFER
   Client B → Server: ANSWER (SDP)
   Server → Client A: ANSWER
   
5. ICE candidate exchange:
   Both clients exchange ICE candidates via server
   
6. Direct connection established:
   P2P WebRTC DataChannel (encrypted)
   Signaling connection downgraded to heartbeat only
```

### Input Sharing Flow

```
1. Input event on Device A:
   Platform layer captures event
   Data layer serializes event
   Security layer encrypts payload
   Connection manager sends via DataChannel
   
2. Transmission:
   WebRTC DataChannel (reliable for keyboard, unreliable for mouse)
   Optional compression for batched events
   
3. Reception on Device B:
   Security layer decrypts payload
   Data layer deserializes event
   Platform layer injects event
   
4. Acknowledgement (optional):
   For critical events (clipboard, confirmations)
```

## Platform-Specific Architecture Details

### Ubuntu Client Architecture

```
┌─────────────────────────────────────────┐
│            GTK/Qt Application           │
├─────────────────────────────────────────┤
│           Input Capture Module          │
│  • XInput2 for high-level events       │
│  • evdev for low-latency raw input     │
│  • uinput for input injection          │
├─────────────────────────────────────────┤
│          Clipboard Module               │
│  • X11 selection (primary, clipboard)  │
│  • Wayland support via portal          │
├─────────────────────────────────────────┤
│          Screen Capture Module          │
│  • XShm for fast capture               │
│  • PipeWire for Wayland                │
└─────────────────────────────────────────┘
```

### Windows Client Architecture

```
┌─────────────────────────────────────────┐
│         WinUI/WPF Application           │
├─────────────────────────────────────────┤
│           Input Capture Module          │
│  • Raw Input API for HID devices       │
│  • Low-level keyboard hooks            │
│  • SendInput for injection             │
├─────────────────────────────────────────┤
│          Clipboard Module               │
│  • Clipboard API                       │
│  • Multiple format support             │
├─────────────────────────────────────────┤
│          Screen Capture Module          │
│  • Desktop Duplication API (DXGI)      │
│  • GDI fallback                        │
└─────────────────────────────────────────┘
```

### Android Client Architecture

```
┌─────────────────────────────────────────┐
│          Jetpack Compose UI             │
├─────────────────────────────────────────┤
│       Accessibility Service             │
│  • Captures input events               │
│  • Requires user permission            │
│  • Runs as foreground service          │
├─────────────────────────────────────────┤
│         Input Injection Module          │
│  • Instrumentation for system apps     │
│  • Accessibility for all apps          │
│  • Requires special permissions        │
├─────────────────────────────────────────┤
│          Clipboard Module               │
│  • ClipboardManager API                │
│  • Content provider for rich content   │
└─────────────────────────────────────────┘
```

## Deployment Architecture

### Cloud Deployment

```
┌─────────────────────────────────────────────────┐
│                Load Balancer                    │
│               (HTTPS/TLS Termination)           │
└──────────────┬────────────────┬─────────────────┘
               │                │
    ┌──────────▼──────┐ ┌──────▼──────────┐
    │  Signaling      │ │  Signaling      │
    │  Server 1       │ │  Server 2       │
    │  (Region A)     │ │  (Region B)     │
    └──────────┬──────┘ └──────┬──────────┘
               │                │
    ┌──────────▼──────┐ ┌──────▼──────────┐
    │  Redis Cluster  │ │  PostgreSQL     │
    │  (Session Store)│ │  (Audit Logs)   │
    └─────────────────┘ └─────────────────┘
               │                │
    ┌──────────▼────────────────▼──────────┐
    │       STUN/TURN Server Cluster       │
    │      (Coturn with Redis backend)     │
    └──────────────────────────────────────┘
```

### Client Deployment

**Package Formats:**
- Ubuntu: `.deb` package for APT, Snap, or Flatpak
- Windows: MSI installer and portable executable
- Android: APK via Google Play and direct download

**Update Mechanism:**
- Auto-update with signature verification
- Delta updates to reduce bandwidth
- Rollback capability on failure

## Scalability Considerations

### Horizontal Scaling
- Signaling servers are stateless
- Session data stored in Redis cluster
- STUN/TURN servers can be added as needed
- Geographic distribution for latency reduction

### Vertical Scaling
- Each signaling server handles ~10K concurrent sessions
- TURN server bandwidth limits per instance
- Database sharding by region

### Cost Optimization
- STUN servers are lightweight and cheap
- TURN servers only used when necessary (~20% of connections)
- Signaling server auto-scaling based on load
- CDN for client downloads

## Fault Tolerance

### Redundancy
- Multiple signaling server instances
- Database replication across regions
- DNS failover between regions
- Client-side retry logic with exponential backoff

### Disaster Recovery
- Daily backups of audit logs
- Session data is ephemeral (not critical)
- Geographic redundancy for TURN servers
- Client-side session caching for reconnection

### Monitoring & Alerting
- Comprehensive metrics collection
- Real-time dashboard for system health
- Automated alerts for anomalies
- Client-side error reporting (opt-in)

## Performance Targets

### Latency (95th percentile)
- Input event processing: <5ms local
- Network transmission: <50ms regional, <200ms global
- End-to-end input lag: <100ms optimal, <300ms acceptable

### Throughput
- Input events: 1000/sec per device
- Clipboard updates: 10/sec per device
- Screen sharing: 30 FPS at 1080p with compression

### Reliability
- 99.9% uptime for signaling service
- 99.99% uptime for STUN service
- Automatic failover within 60 seconds

This architecture provides a robust foundation that meets all core requirements while maintaining security, performance, and scalability.