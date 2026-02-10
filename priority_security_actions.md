# Priority Security Actions

## Immediate (Blocking)
1. **Fix Weak RNG in Session Codes** (`SessionCode.Generate()`)
   - Replace `new Random()` with `RandomNumberGenerator.Create()`
   - File: `CrossInputShare.Core/Models/SessionCode.cs`

2. **Implement Key Exchange Protocol**
   - Add X25519 key exchange and HKDF key derivation
   - Integrate with `IEncryptionService.Initialize()`
   - Files: New `KeyExchangeService` class

3. **Increase Session Code Entropy**
   - Extend random part from 6 to 8 characters (32^8 = ~1.1e12 possibilities)
   - Update `SessionCode` constants and validation

4. **Implement Server-Side Session Management**
   - Add session expiration (10 minutes)
   - One-time use invalidation
   - Rate limiting (max 5 attempts per minute per IP)

## Short-Term (Next Sprint)
5. **Strengthen Checksum Algorithm**
   - Replace simple sum with Luhn mod N or truncated HMAC
   - If server-side generation, use HMAC with server secret

6. **Improve Device Fingerprint Security**
   - Add per-installation random salt
   - Use structured serialization (JSON) instead of pipe concatenation
   - Display 12+ characters for verification

7. **Add Authorization Checks**
   - Enforce host/peer roles in `SessionInfo`
   - Validate device identity before operations

8. **Fix Fingerprint Display**
   - Show at least 12 hex characters
   - Provide "Show full fingerprint" option

## Medium-Term (Before Beta)
9. **Implement Network Security**
   - WebRTC signaling with certificate pinning
   - Rate limiting and connection throttling
   - STUN/TURN server configuration

10. **Comprehensive Input Validation**
    - Validate all network messages, file paths, clipboard content
    - Implement size limits and sanitization

11. **Secure Logging**
    - Structured logging without sensitive data
    - Log security events for audit

12. **Platform-Specific Hardening**
    - Windows: Least privilege, secure input hooks
    - Ubuntu: XInput, sandboxing
    - Android: Minimal Accessibility Service scope

## Long-Term (Continuous)
13. **Regular Security Audits**
    - Quarterly code reviews
    - Dependency vulnerability scanning

14. **Penetration Testing**
    - Engage third-party security testers annually

15. **Bug Bounty Program**
    - Establish responsible disclosure process

## Quick Fixes (Code Examples)

### Fix SessionCode.Generate()
```csharp
public static SessionCode Generate()
{
    var rng = RandomNumberGenerator.Create();
    var randomPart = new StringBuilder(RandomLength);
    
    for (int i = 0; i < RandomLength; i++)
    {
        byte[] randomByte = new byte[1];
        rng.GetBytes(randomByte);
        randomPart.Append(ValidChars[randomByte[0] % ValidChars.Length]);
    }
    
    string randomString = randomPart.ToString();
    char checksum = CalculateChecksum(randomString);
    
    return new SessionCode(randomString + checksum);
}
```

### Add Installation Salt to Fingerprint
```csharp
public static DeviceFingerprint Generate(string platformInfo, string machineId, string installationId, byte[] salt = null)
{
    // Generate salt if not provided
    if (salt == null)
    {
        salt = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(salt);
        // Store salt in local configuration
    }
    
    // Use structured serialization
    var data = new {
        Platform = platformInfo,
        Machine = machineId,
        Installation = installationId,
        Salt = Convert.ToBase64String(salt)
    };
    
    string json = JsonSerializer.Serialize(data);
    
    using (var sha256 = SHA256.Create())
    {
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return new DeviceFingerprint(BytesToHex(hashBytes));
    }
}
```