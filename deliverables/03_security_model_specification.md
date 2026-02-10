# Deliverable 3: Security Model Specification

## 1. Security Principles

### 1.1 Core Principles
1. **Zero Trust Architecture**: Verify explicitly, never trust implicitly
2. **Defense in Depth**: Multiple overlapping security controls
3. **Least Privilege**: Minimum access required for functionality
4. **Privacy by Design**: Data minimization and user control
5. **Transparency**: Users informed of security state and actions

### 1.2 Threat Model

**Assumed Threats:**
1. Network eavesdroppers (MITM attacks)
2. Malicious devices joining sessions
3. Compromised signaling servers
4. Malware on client devices
5. Denial of service attacks
6. Social engineering attacks
7. Physical device compromise

**Out of Scope:**
1. Operating system kernel exploits
2. Hardware-level attacks
3. Physical theft with unlocked devices
4. Zero-day exploits in dependencies

## 2. Authentication & Authorization

### 2.1 Device Fingerprinting

**Fingerprint Components:**
```
Fingerprint = SHA256(
  Platform_ID +
  Machine_ID +
  Installation_ID + 
  Public_Key +
  Salt
)
```

**Components Details:**
1. **Platform_ID**: OS name + version + architecture
2. **Machine_ID**: Platform-specific unique identifier:
   - Linux: `/etc/machine-id`
   - Windows: MachineGUID from registry
   - Android: ANDROID_ID + Build.SERIAL
3. **Installation_ID**: Random UUID generated on first install
4. **Public_Key**: Ed25519 public key (generated per installation)
5. **Salt**: Random 32 bytes stored securely

**Properties:**
- Unique per device installation
- Persists across app updates
- Changes on reinstall
- Non-PII (no user identification)

### 2.2 Session Code Security

**Generation:**
```python
def generate_session_code():
    # Cryptographically secure random
    random_bytes = os.urandom(4)
    # 6-character alphanumeric (36^6 ≈ 2.1B combos)
    code = base36_encode(random_bytes).rjust(6, '0')[:6]
    # Add checksum digit
    checksum = crc8(code) % 36
    return code + base36_encode(checksum)
```

**Properties:**
- 7 characters total (6 random + 1 checksum)
- 36^6 ≈ 2.1 billion possible codes
- Time-limited: 10 minute validity
- One-time use: Invalid after first successful join
- Case-insensitive input
- Checksum prevents typos

**Storage & Validation:**
```python
SESSION_DB = {
    "A1B2C3D": {
        "created_at": timestamp,
        "expires_at": timestamp + 600,
        "creator_fp": "fp_abc123...",
        "peers": [],
        "used": False
    }
}
```

### 2.3 Manual Verification Process

**Verification Flow:**
1. Both devices display fingerprint (truncated to 8 chars)
2. Users verbally compare or scan QR code
3. Both users must explicitly confirm
4. Confirmation is recorded in audit log

**UI Display:**
```
Device Fingerprint Verification
────────────────────────────────
Your Device:     A1B2C3D4
Remote Device:   E5F6G7H8

☐ I confirm the fingerprints match
☐ I trust this device

[Cancel] [Verify & Connect]
```

**Security Implications:**
- Prevents MITM attacks without trusted third party
- Users become part of security chain
- Clear, simple interface reduces errors

## 3. Encryption & Data Protection

### 3.1 End-to-End Encryption

**Cryptographic Suite:**
- Key Exchange: X25519 (Elliptic Curve Diffie-Hellman)
- Encryption: AES-256-GCM (Authenticated Encryption)
- Hash: SHA-256
- Key Derivation: HKDF-SHA256
- Random: System CSPRNG (/dev/urandom, BCryptGenRandom, SecureRandom)

**Perfect Forward Secrecy:**
- Ephemeral key pairs for each session
- Session keys derived from ephemeral shared secret
- Keys never stored persistently
- Compromised long-term keys don't affect past sessions

### 3.2 Key Management Lifecycle

**Key Generation:**
```python
class KeyManager:
    def __init__(self):
        # Long-term identity key (Ed25519)
        self.identity_key = Ed25519.generate()
        
        # Ephemeral session key (X25519)
        self.ephemeral_key = X25519.generate()
        
    def derive_session_keys(self, peer_public_key):
        # Compute shared secret
        shared_secret = self.ephemeral_key.exchange(peer_public_key)
        
        # Derive keys using HKDF
        salt = b"inputshare-session-keys-v1"
        info = self.session_id + b"|" + self.device_fingerprint
        key_material = HKDF(shared_secret, salt, info, 64)
        
        return {
            'encryption_key': key_material[0:32],
            'authentication_key': key_material[32:64]
        }
```

**Key Storage:**
- Identity private key: Platform secure storage
  - Linux: Keyring service
  - Windows: Credential Manager
  - Android: Keystore
- Session keys: Memory only, never written to disk
- Key material zeroed after use

### 3.3 Message Encryption Format

**Encrypted Message Structure:**
```
┌─────────────────────────────────────────────────┐
│                 Header (8 bytes)                 │
│   Version (1B) | Type (1B) | Seq (2B) | ...     │
├─────────────────────────────────────────────────┤
│               Nonce (12 bytes)                   │
├─────────────────────────────────────────────────┤
│            Ciphertext (variable)                 │
├─────────────────────────────────────────────────┤
│          Authentication Tag (16 bytes)           │
└─────────────────────────────────────────────────┘
```

**Encryption Process:**
1. Generate random 12-byte nonce
2. Encrypt payload: `ciphertext, tag = AES-256-GCM.encrypt(key, nonce, plaintext, associated_data)`
3. Associated data includes header for integrity

**Decryption Process:**
1. Verify tag matches ciphertext
2. Decrypt: `plaintext = AES-256-GCM.decrypt(key, nonce, ciphertext, tag, associated_data)`
3. Reject if authentication fails

## 4. Access Control & Rate Limiting

### 4.1 Connection Throttling

**Rate Limits per Connection:**
```python
RATE_LIMITS = {
    'input_events': {
        'max_per_second': 1000,
        'burst': 5000,
        'action': 'throttle'  # Queue excess, drop if full
    },
    'clipboard_updates': {
        'max_per_second': 10,
        'burst': 50,
        'action': 'delay'  # Always deliver, just slow down
    },
    'screen_frames': {
        'max_per_second': 30,
        'burst': 90,
        'action': 'drop'  # Drop excess frames
    },
    'control_messages': {
        'max_per_second': 100,
        'burst': 1000,
        'action': 'queue'  # Always deliver
    }
}
```

**Implementation:**
- Token bucket algorithm
- Separate buckets per message type
- Adaptive limits based on network conditions

### 4.2 Device & Session Limits

**Global Limits:**
- Maximum 5 devices per session
- Maximum 3 concurrent sessions per device
- Maximum 10 session creations per hour per IP
- Maximum 30 session joins per hour per IP

**Enforcement:**
```python
def check_session_limits(device_fingerprint, ip_address):
    # Check device limits
    device_sessions = db.get_active_sessions(device_fingerprint)
    if len(device_sessions) >= 3:
        raise LimitExceeded("Too many concurrent sessions")
    
    # Check IP limits
    ip_creations = db.get_session_creations_last_hour(ip_address)
    if ip_creations >= 10:
        raise LimitExceeded("Too many session creations")
```

### 4.3 Geographic Restrictions

**Optional Configuration:**
```json
{
  "geofencing": {
    "allowed_countries": ["US", "CA", "GB", "DE", "FR"],
    "blocked_countries": [],
    "enable_vpn_detection": true,
    "strict_mode": false
  }
}
```

**Implementation:**
- IP to country mapping (MaxMind GeoLite2)
- VPN detection via commercial APIs
- Configurable by enterprise deployments

## 5. Audit Logging & Monitoring

### 5.1 Audit Event Types

**Security Events:**
1. Session created/joined/terminated
2. Device fingerprint verification
3. Encryption key exchange
4. Rate limit violations
5. Permission denials
6. Error conditions

**Privacy Events:**
1. Clipboard access (read/write)
2. Screen sharing start/stop
3. File transfer initiated
4. Input event statistics (counts, not content)

### 5.2 Log Format

**Structured Log Entry:**
```json
{
  "timestamp": "2026-02-09T19:36:00Z",
  "event_id": "evt_abc123",
  "event_type": "session_created",
  "severity": "info",
  "device_fingerprint": "fp_abc123...",
  "session_id": "sess_def456",
  "ip_address": "203.0.113.1",
  "user_agent": "InputShare/1.0.0 (Ubuntu)",
  "data": {
    "session_code": "A1B2C3",
    "expires_at": "2026-02-09T19:46:00Z"
  }
}
```

### 5.3 Log Storage & Retention

**Client-Side Logs:**
- Local storage, encrypted at rest
- 7-day retention
- Auto-pruning of old entries
- Export capability for debugging

**Server-Side Logs:**
- Centralized logging infrastructure
- 90-day retention minimum
- Encrypted in transit and at rest
- Access controlled with audit trail

**Privacy Protection:**
- No PII in logs
- IP addresses anonymized after 24 hours
- Device fingerprints hashed with salt
- Logs never contain input content

## 6. User Confirmation System

### 6.1 Confirmation Triggers

**Mandatory Confirmations:**
1. First connection to new device
2. Clipboard read access request
3. Screen sharing initiation
4. File transfer requests
5. Administrative actions (kick device, change settings)

**Optional Confirmations (user configurable):**
1. Clipboard write notifications
2. High-volume input sessions
3. Long-duration connections
4. Geographic location changes

### 6.2 Confirmation Protocol

**Request Message:**
```json
{
  "event_type": "confirmation_request",
  "request_id": "req_123456",
  "action": "clipboard_read",
  "requester": {
    "device_fingerprint": "fp_abc123...",
    "device_name": "John's Laptop"
  },
  "context": {
    "clipboard_format": "text/plain",
    "preview": "Hello...",  // First 50 chars only
    "timestamp": 1678901234567
  },
  "timeout_ms": 30000,
  "default_action": "deny"
}
```

**Response Handling:**
- Modal dialog with clear options
- 30-second timeout (auto-deny)
- Default to safest option
- Audit log entry regardless of choice

**UI Design Principles:**
- Clear, unambiguous language
- Prominent security indicators
- Consistent placement of approve/deny buttons
- No dark patterns or trick questions

## 7. Anti-Abuse Measures

### 7.1 Anomaly Detection

**Behavioral Analysis:**
```python
class AnomalyDetector:
    def analyze_session(self, session_data):
        anomalies = []
        
        # Input pattern analysis
        if self.is_automated_pattern(session_data.input_events):
            anomalies.append("automated_input")
            
        # Geographic velocity
        if self.impossible_travel(session_data.locations):
            anomalies.append("impossible_travel")
            
        # Device fingerprint changes
        if self.fingerprint_drift(session_data.device_info):
            anomalies.append("device_changed")
            
        # Connection patterns
        if self.rapid_reconnections(session_data.connections):
            anomalies.append("connection_flooding")
            
        return anomalies
```

**Responses to Anomalies:**
1. **Warning**: Log event, notify user
2. **Throttling**: Slow down suspicious connections
3. **Blocking**: Temporarily block suspicious devices
4. **Termination**: End session with explanation

### 7.2 Reputation System

**Device Reputation Factors:**
- Age of installation
- Successful verifications
- Rate limit compliance
- Geographic consistency
- Report history

**Reputation Scores:**
- High: Trusted, fewer confirmations needed
- Medium: Standard security checks
- Low: Enhanced monitoring, more confirmations
- Banned: Blocked from service

## 8. Platform-Specific Security

### 8.1 Linux (Ubuntu) Security

**Sandboxing:**
- Flatpak/Snap confinement
- Namespace isolation
- Seccomp filters
- AppArmor/SELinux policies

**Permissions:**
```ini
[Flatpak Permissions]
--socket=x11
--device=evdev
--talk-name=org.freedesktop.secrets
--filesystem=home:ro
```

**Input Security:**
- uinput module loaded with restrictive permissions
- X11 access via MIT-SHM only when needed
- Wayland portal integration for modern systems

### 8.2 Windows Security

**Execution Protection:**
- Code signing with EV certificate
- SmartScreen filter approval
- Windows Defender application control
- Mandatory ASLR and DEP

**Permission Model:**
- Administrator elevation only for driver installation
- User-level operation thereafter
- Protected processes where possible
- Windows Hello integration for confirmations

### 8.3 Android Security

**Permission Model:**
```xml
<uses-permission android:name="android.permission.BIND_ACCESSIBILITY_SERVICE"/>
<uses-permission android:name="android.permission.FOREGROUND_SERVICE"/>
<uses-permission android:name="android.permission.INTERNET"/>
```

**Security Hardening:**
- Target API level 30+ (Android 11)
- Hardware-backed keystore for keys
- Biometric integration for confirmations
- Not running when screen is locked

## 9. Incident Response Plan

### 9.1 Detection & Classification

**Security Event Classification:**
- Level 1: Informational (logged)
- Level 2: Suspicious (investigate)
- Level 3: Malicious (contain)
- Level 4: Critical (emergency response)

**Detection Mechanisms:**
- Real-time log analysis
- Automated anomaly detection
- User reports
- External threat intelligence

### 9.2 Response Procedures

**Containment Steps:**
1. Isolate affected sessions
2. Revoke session codes
3. Block malicious devices
4. Notify affected users

**Eradication & Recovery:**
1. Identify root cause
2. Apply patches/fixes
3. Restore from clean backups
4. Monitor for recurrence

**Communication Plan:**
- Internal: Immediate team notification
- Users: Transparent disclosure (affected users only)
- Public: Optional disclosure based on severity

## 10. Compliance & Certification

### 10.1 Standards Compliance

**Security Standards:**
- OWASP Application Security Verification Standard (ASVS)
- NIST Cybersecurity Framework
- ISO/IEC 27001 Information Security Management
- GDPR (privacy requirements)

**Cryptographic Standards:**
- FIPS 140-2 validated cryptography (where applicable)
- NIST SP 800-56A key establishment
- RFC 7748 Elliptic Curves for Security

### 10.2 Third-Party Assessments

**Regular Security Activities:**
- Annual penetration testing
- Quarterly vulnerability scans  
- Continuous dependency monitoring
- Bug bounty program

**Certification Goals:**
- SOC 2 Type II certification
- ISO 27001 certification
- Common Criteria evaluation (EAL2+)

## 11. Future Security Enhancements

### 11.1 Quantum Resistance
- Post-quantum cryptography migration plan
- Hybrid key exchange (X25519 + Kyber)
- Regular review of cryptographic advancements

### 11.2 Decentralized Trust
- Web of trust for device verification
- Blockchain-based audit logs
- Distributed signaling servers

### 11.3 Hardware Security
- TPM integration for key storage
- Hardware security keys (Yubikey) for authentication
- Secure enclave utilization (Apple Secure Enclave, Android Titan M)

### 11.4 AI-Powered Protection
- Machine learning for anomaly detection
- Behavioral biometrics for device verification
- Predictive threat intelligence

This security model provides comprehensive protection against anticipated threats while maintaining usability and performance. The layered approach ensures that failure of any single control doesn't compromise the entire system.