# Security Best Practices Checklist

## Cryptography
- [ ] Use cryptographically secure random number generators (`RandomNumberGenerator`) everywhere
- [ ] Implement proper key exchange (X25519) and key derivation (HKDF)
- [ ] Use authenticated encryption (AES-GCM or ChaCha20-Poly1305) for all sensitive data
- [ ] Never reuse nonces with the same key
- [ ] Implement forward secrecy (key rotation per session)
- [ ] Store keys in platform secure storage (DPAPI, Keystore, keyring)
- [ ] Zeroize sensitive memory after use

## Authentication & Session Management
- [ ] Session codes must have sufficient entropy (≥ 40 bits)
- [ ] Implement server-side rate limiting (max 5 attempts per minute)
- [ ] Session expiration (10 minutes for unclaimed sessions)
- [ ] One-time use session codes
- [ ] Manual device fingerprint verification before sensitive operations
- [ ] Clear visual indicators for verification status

## Input Validation & Sanitization
- [ ] Validate all input (network messages, user input, file paths)
- [ ] Use whitelist validation where possible
- [ ] Sanitize clipboard content (strip scripts, limit size)
- [ ] Prevent directory traversal in file operations
- [ ] Implement size limits for all data transfers

## Network Security
- [ ] Use TLS 1.3 for all network communications
- [ ] Implement certificate pinning for signaling server
- [ ] Rate limit connection attempts per IP
- [ ] Use WebRTC with DTLS-SRTP for P2P connections
- [ ] Consider additional application-layer encryption
- [ ] Log security events (without sensitive data)

## Platform-Specific Security
### Windows
- [ ] Run with minimal privileges (non-admin when possible)
- [ ] Secure input hook installation (admin only for setup)
- [ ] Validate window ownership before injecting input
- [ ] Use Windows Defender exclusion if necessary

### Ubuntu/Linux
- [ ] Use XInput extension instead of low-level X11 calls
- [ ] Sandbox application (Flatpak/Snap)
- [ ] Request permissions via polkit
- [ ] Secure configuration file permissions

### Android
- [ ] Use Accessibility Service with minimal scope
- [ ] Require explicit user enablement
- [ ] Foreground service with persistent notification
- [ ] Request minimal permissions
- [ ] Use Android Keystore for key storage

## Code Quality & Development
- [ ] Enable .NET security analyzers
- [ ] Treat security warnings as errors
- [ ] Regular dependency vulnerability scanning
- [ ] Security code reviews for all changes
- [ ] Implement comprehensive logging (no sensitive data)
- [ ] Secure exception handling (no stack traces in production)

## User Experience & Privacy
- [ ] Explicit user consent for sensitive operations
- [ ] Clear security indicators (encryption status, verification)
- [ ] Easy session revocation
- [ ] Automatic session timeout after inactivity
- [ ] Data minimization (collect only necessary information)
- [ ] Privacy-preserving fingerprint generation

## Monitoring & Response
- [ ] Log security events for audit
- [ ] Monitor for anomalous behavior
- [ ] Establish incident response plan
- [ ] Regular security audits (quarterly)
- [ ] Penetration testing before major releases
- [ ] Bug bounty program or responsible disclosure process

## Compliance
- [ ] Document security architecture
- [ ] Maintain vulnerability register
- [ ] Regular third-party security assessments
- [ ] GDPR/CCPA compliance tools if applicable
- [ ] Export control compliance