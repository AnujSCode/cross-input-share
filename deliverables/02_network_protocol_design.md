# Deliverable 2: Network Protocol Design

## Protocol Stack Overview

```
┌─────────────────────────────────────────────────┐
│              Application Layer                   │
│  • Input event serialization                    │
│  • Clipboard synchronization                    │
│  • Screen sharing                              │
│  • Session management                           │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│                Security Layer                    │
│  • End-to-end encryption (AES-256-GCM)         │
│  • Message authentication                       │
│  • Key exchange (X25519)                       │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│              Transport Layer                     │
│  • WebRTC DataChannel (primary)                │
│  • SCTP over DTLS over UDP                     │
│  • Fallback: TURN relay                        │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│              Signaling Layer                     │
│  • WebSocket over TLS                          │
│  • JSON message format                         │
│  • Session negotiation                         │
└─────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────┐
│                Network Layer                     │
│  • UDP for WebRTC                              │
│  • TCP for WebSocket                           │
│  • STUN/TURN for NAT traversal                 │
└─────────────────────────────────────────────────┘
```

## 1. Signaling Protocol (WebSocket)

### 1.1 Connection Establishment

**WebSocket Endpoint:** `wss://signal.inputshare.example.com/v1/ws`

**Authentication:** None initially (session codes provide auth)

**Message Format:** JSON with type field

```json
{
  "type": "message_type",
  "seq": 12345,           // Sequence number (monotonically increasing)
  "timestamp": 1678901234567,
  "session_id": "abc123", // Optional, after session establishment
  "data": { ... }         // Type-specific data
}
```

### 1.2 Message Types

#### Session Creation
```json
{
  "type": "create_session",
  "data": {
    "device_fingerprint": "fp_abc123...",
    "platform": "ubuntu",
    "version": "1.0.0",
    "capabilities": ["keyboard", "mouse", "clipboard"]
  }
}

// Response:
{
  "type": "session_created",
  "data": {
    "session_code": "A1B2C3",      // 6-digit alphanumeric
    "session_id": "sess_abc123",
    "expires_at": 1678901294567,   // Unix timestamp (10 minutes)
    "device_fingerprint": "fp_def456..." // Other device's fingerprint
  }
}
```

#### Session Join
```json
{
  "type": "join_session",
  "data": {
    "session_code": "A1B2C3",
    "device_fingerprint": "fp_def456...",
    "platform": "windows",
    "version": "1.0.0"
  }
}

// Response (to both clients):
{
  "type": "peer_joined",
  "data": {
    "session_id": "sess_abc123",
    "peers": [
      {
        "device_fingerprint": "fp_abc123...",
        "platform": "ubuntu",
        "connection_id": "conn_1"
      },
      {
        "device_fingerprint": "fp_def456...", 
        "platform": "windows",
        "connection_id": "conn_2"
      }
    ]
  }
}
```

#### WebRTC Offer/Answer Exchange
```json
{
  "type": "webrtc_offer",
  "data": {
    "connection_id": "conn_1",
    "sdp": "v=0\r\no=- 123456 2 IN IP4 127.0.0.1\r\n..."
  }
}

{
  "type": "webrtc_answer",
  "data": {
    "connection_id": "conn_2",
    "sdp": "v=0\r\no=- 789012 2 IN IP4 127.0.0.1\r\n..."
  }
}
```

#### ICE Candidate Exchange
```json
{
  "type": "ice_candidate",
  "data": {
    "connection_id": "conn_1",
    "candidate": "candidate:1 1 UDP 2130706431 192.168.1.100 5000 typ host"
  }
}
```

#### Heartbeat
```json
{
  "type": "heartbeat",
  "data": {
    "timestamp": 1678901234567
  }
}

// Response:
{
  "type": "heartbeat_ack",
  "data": {
    "timestamp": 1678901234567,
    "server_time": 1678901235000
  }
}
```

### 1.3 Error Messages
```json
{
  "type": "error",
  "data": {
    "code": "invalid_session",
    "message": "Session code not found or expired",
    "retry_after": 30  // Optional: seconds to wait before retry
  }
}
```

**Error Codes:**
- `invalid_session`: Session code wrong/expired
- `session_full`: Maximum 5 devices reached
- `rate_limited`: Too many requests
- `device_banned`: Device fingerprint banned
- `version_unsupported`: Client version too old
- `internal_error`: Server error

### 1.4 Rate Limiting

**Headers in WebSocket upgrade response:**
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 99
X-RateLimit-Reset: 1678901300
```

**Buckets:**
- `session_create`: 10 per hour per IP
- `session_join`: 30 per hour per IP  
- `messages`: 100 per minute per connection
- `ice_candidates`: 50 per minute per connection

## 2. WebRTC DataChannel Protocol

### 2.1 DataChannel Configuration

**Primary Channel (Reliable, Ordered):**
- Label: `control`
- Protocol: `inputshare-control-v1`
- Negotiated: true
- Id: 0
- Purpose: Control messages, clipboard, confirmations

**Secondary Channel (Unreliable, Unordered):**
- Label: `input`
- Protocol: `inputshare-input-v1`
- Negotiated: true  
- Id: 1
- Purpose: Input events (mouse movements, keystrokes)

**Tertiary Channel (Reliable, Ordered - Optional):**
- Label: `screen`
- Protocol: `inputshare-screen-v1`
- Negotiated: true
- Id: 2
- Purpose: Screen sharing data

### 2.2 Message Framing

**Header (8 bytes):**
```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|    Version    |     Type      |           Sequence            |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                          Timestamp                           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                        Payload Length                        |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

**Fields:**
- `Version` (1 byte): Protocol version (0x01)
- `Type` (1 byte): Message type (see below)
- `Sequence` (2 bytes): Monotonically increasing per channel
- `Timestamp` (4 bytes): Unix timestamp in milliseconds
- `Payload Length` (4 bytes): Length of encrypted payload

**Message Types:**
- `0x01`: INPUT_EVENT
- `0x02`: CLIPBOARD_UPDATE
- `0x03`: SCREEN_FRAME
- `0x04`: KEEPALIVE
- `0x05`: CONFIRMATION_REQUEST
- `0x06`: CONFIRMATION_RESPONSE
- `0x07`: AUDIT_LOG
- `0x08`: ERROR
- `0x09`: STATS_REPORT
- `0x0A`: FLOW_CONTROL

### 2.3 Encryption

**Encryption Process:**
1. Generate random 12-byte nonce for each message
2. Encrypt payload with AES-256-GCM using session key and nonce
3. Append authentication tag (16 bytes)
4. Final message: Header + Nonce (12B) + Ciphertext + Tag (16B)

**Decryption Process:**
1. Extract nonce and tag
2. Decrypt with session key
3. Verify authentication tag
4. Process plaintext payload

## 3. Application-Level Protocols

### 3.1 Input Event Protocol

**Mouse Movement (Relative):**
```json
{
  "event_type": "mouse_move",
  "dx": 10,
  "dy": -5,
  "buttons": 0,      // Bitmask: 1=left, 2=right, 4=middle
  "timestamp": 1678901234567
}
```

**Mouse Click:**
```json
{
  "event_type": "mouse_click",
  "button": "left",  // "left", "right", "middle"
  "action": "down",  // "down", "up", "click"
  "timestamp": 1678901234567
}
```

**Keyboard Event:**
```json
{
  "event_type": "keyboard",
  "keycode": 65,     // Platform-agnostic keycode
  "scancode": 30,    // Platform-specific scancode
  "action": "down",  // "down", "up", "press"
  "modifiers": 5,    // Bitmask: 1=Ctrl, 2=Shift, 4=Alt, 8=Meta
  "timestamp": 1678901234567
}
```

**Wheel Event:**
```json
{
  "event_type": "wheel",
  "dx": 0,
  "dy": 120,         // Positive = scroll up, negative = down
  "timestamp": 1678901234567
}
```

### 3.2 Clipboard Protocol

**Text Clipboard:**
```json
{
  "event_type": "clipboard_update",
  "format": "text/plain",
  "data": "Hello World",
  "seq": 123,
  "timestamp": 1678901234567
}
```

**Rich Text Clipboard:**
```json
{
  "event_type": "clipboard_update",
  "format": "text/html",
  "data": "<b>Hello</b> World",
  "seq": 124,
  "timestamp": 1678901234567
}
```

**Image Clipboard:**
```json
{
  "event_type": "clipboard_update",
  "format": "image/png",
  "data_base64": "iVBORw0KGgoAAAANSUhEUg...", // Base64 encoded
  "size": 10240,
  "seq": 125,
  "timestamp": 1678901234567
}
```

### 3.3 Screen Sharing Protocol

**Frame Header:**
```json
{
  "frame_type": "keyframe",  // "keyframe" or "delta"
  "width": 1920,
  "height": 1080,
  "format": "nv12",         // "nv12", "rgb", "rgba"
  "timestamp": 1678901234567,
  "seq": 1001,
  "compression": "lz4",     // "none", "lz4", "zstd"
  "data_size": 1536000
}
```

**Region Update:**
```json
{
  "frame_type": "delta",
  "regions": [
    {
      "x": 100,
      "y": 100,
      "width": 200,
      "height": 200,
      "data": "..."  // Compressed pixel data
    }
  ],
  "timestamp": 1678901234567,
  "seq": 1002
}
```

### 3.4 Control Protocol

**Confirmation Request:**
```json
{
  "event_type": "confirmation_request",
  "request_id": "req_123",
  "action": "clipboard_access",
  "device_fingerprint": "fp_abc123...",
  "timestamp": 1678901234567,
  "timeout": 30000  // Milliseconds
}
```

**Confirmation Response:**
```json
{
  "event_type": "confirmation_response",
  "request_id": "req_123",
  "approved": true,
  "timestamp": 1678901234567
}
```

**Audit Log Entry:**
```json
{
  "event_type": "audit_log",
  "log_level": "info",  // "debug", "info", "warn", "error"
  "category": "security",
  "message": "Device fingerprint verified",
  "device_fingerprint": "fp_abc123...",
  "timestamp": 1678901234567,
  "data": { ... }  // Optional additional context
}
```

## 4. NAT Traversal Protocol

### 4.1 STUN Usage

**Client Configuration:**
- Primary STUN servers: `stun1.inputshare.example.com:3478`
- Secondary STUN servers: `stun2.inputshare.example.com:3478`
- Tertiary: Public STUN servers (fallback)

**Message Flow:**
1. Client sends STUN Binding Request to server
2. Server responds with mapped address
3. Client uses address for ICE candidates

### 4.2 TURN Usage

**Client Configuration:**
- TURN servers: `turn.inputshare.example.com:3478`
- Credentials: Temporary tokens issued by signaling server
- Allocation lifetime: 1 hour

**Authentication:**
```json
{
  "type": "turn_credentials",
  "data": {
    "username": "temp_user_123",
    "password": "temp_pass_abc",
    "ttl": 3600,
    "uris": [
      "turn:turn.inputshare.example.com:3478?transport=udp",
      "turn:turn.inputshare.example.com:3478?transport=tcp"
    ]
  }
}
```

## 5. Flow Control & Congestion Management

### 5.1 Bandwidth Estimation

**Client Statistics Report:**
```json
{
  "event_type": "stats_report",
  "timestamp": 1678901234567,
  "metrics": {
    "rtt": 45,          // Round-trip time in ms
    "jitter": 5,        // Jitter in ms
    "packet_loss": 0.01, // Packet loss ratio
    "bandwidth_up": 5000,   // Estimated upload bandwidth (kbps)
    "bandwidth_down": 10000, // Estimated download bandwidth (kbps)
    "queued_messages": 10   // Messages in send queue
  }
}
```

### 5.2 Adaptive Quality

**Screen Sharing Quality Levels:**
1. **Low:** 640x480 @ 15 FPS, high compression
2. **Medium:** 1280x720 @ 20 FPS, medium compression  
3. **High:** 1920x1080 @ 30 FPS, low compression
4. **Adaptive:** Dynamically adjust based on bandwidth

**Quality Switch Signal:**
```json
{
  "event_type": "quality_change",
  "new_quality": "medium",
  "reason": "bandwidth_low",
  "timestamp": 1678901234567
}
```

### 5.3 Message Prioritization

**Priority Levels:**
1. **Critical:** Control messages, confirmations (highest)
2. **High:** Keyboard events, clipboard
3. **Medium:** Mouse clicks, wheel events
4. **Low:** Mouse movements, screen delta frames (lowest)

**Queue Management:**
- Separate queues per priority level
- Lower priority messages dropped under congestion
- Critical messages always delivered

## 6. Protocol Versioning & Compatibility

### 6.1 Version Negotiation

**Client Announcement:**
```json
{
  "type": "client_hello",
  "data": {
    "version": "1.0.0",
    "supported_versions": ["1.0.0", "0.9.0"],
    "features": ["input", "clipboard", "screen_optional"]
  }
}
```

**Server Response:**
```json
{
  "type": "server_hello", 
  "data": {
    "version": "1.0.0",
    "required_features": ["input", "clipboard"]
  }
}
```

### 6.2 Backward Compatibility

- Major version changes break compatibility
- Minor versions add optional features
- Patch versions are fully compatible
- Deprecation warnings for old features

## 7. Security Protocol Details

### 7.1 Key Exchange (X25519)

**Flow:**
1. Each client generates ephemeral keypair
2. Exchange public keys via signaling server (encrypted)
3. Compute shared secret using X25519
4. Derive encryption keys using HKDF-SHA256

**Key Derivation:**
```
salt = "inputshare-key-derivation-1.0"
info = session_id + "|" + device_fingerprints
shared_secret = X25519(privA, pubB)
key_material = HKDF-SHA256(shared_secret, salt, info, 64)
enc_key = key_material[0:32]      // AES-256 key
auth_key = key_material[32:64]    // Future use
```

### 7.2 Certificate Pinning

**Client Configuration:**
- Hardcoded root certificates for signaling server
- Public key pinning for WebSocket TLS
- Certificate transparency logs validation

## 8. Performance Optimizations

### 8.1 Message Batching

**Batched Input Events:**
```json
{
  "event_type": "batch",
  "events": [
    {"type": "mouse_move", "dx": 5, "dy": 0},
    {"type": "mouse_move", "dx": 5, "dy": 0},
    {"type": "keyboard", "keycode": 65, "action": "down"}
  ],
  "count": 3,
  "timestamp": 1678901234567
}
```

### 8.2 Delta Encoding

**Mouse Movement Deltas:**
- Send only changes from last position
- Accumulate small movements into larger updates
- Threshold-based sending (minimum 2px movement)

### 8.3 Compression

**Algorithm Selection:**
- Text: LZ4 (fast) or Brotli (dense)
- Binary: LZ4 or Zstandard
- Images: Already compressed (PNG/JPEG)

**Context:**
```json
{
  "compression": "lz4",
  "original_size": 1024,
  "compressed_size": 512,
  "ratio": 0.5
}
```

This protocol design provides a robust, secure, and efficient foundation for cross-platform input sharing with comprehensive security measures and performance optimizations.