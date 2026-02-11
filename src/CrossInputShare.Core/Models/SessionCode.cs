using System;
using System.Security.Cryptography;
using System.Text;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Represents a session code for authentication between devices.
    /// Format: 8 random characters + 1 checksum character = 9 characters total.
    /// The checksum ensures basic validation to catch typos.
    /// Uses cryptographically secure random number generation.
    /// </summary>
    public class SessionCode
    {
        private const int RandomLength = 8; // Increased from 6 to 8 for more entropy
        private const int TotalLength = 9;  // Updated to match new length
        private const string ValidChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Removed ambiguous characters
        private const int ValidCharsCount = 32; // Length of ValidChars string
        
        private readonly string _code;

        /// <summary>
        /// Creates a new session code with the specified value.
        /// Validates the code format and checksum.
        /// </summary>
        /// <param name="code">The 9-character session code</param>
        /// <exception cref="ArgumentException">Thrown if code is invalid</exception>
        public SessionCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Session code cannot be null or empty", nameof(code));
            
            if (code.Length != TotalLength)
                throw new ArgumentException($"Session code must be {TotalLength} characters long", nameof(code));
            
            // Validate all characters are valid
            foreach (char c in code)
            {
                if (!ValidChars.Contains(c))
                    throw new ArgumentException($"Invalid character '{c}' in session code. Valid characters are: {ValidChars}", nameof(code));
            }
            
            // Validate checksum
            if (!ValidateChecksum(code))
                throw new ArgumentException("Invalid checksum in session code", nameof(code));
            
            _code = code.ToUpperInvariant();
        }

        /// <summary>
        /// Generates a new cryptographically secure random session code.
        /// Uses RandomNumberGenerator for secure randomness.
        /// </summary>
        public static SessionCode Generate()
        {
            // Use cryptographically secure random number generator
            using (var rng = RandomNumberGenerator.Create())
            {
                var randomPart = new StringBuilder(RandomLength);
                byte[] randomBytes = new byte[RandomLength];
                
                // Generate secure random bytes
                rng.GetBytes(randomBytes);
                
                // Convert bytes to characters from our valid character set
                for (int i = 0; i < RandomLength; i++)
                {
                    // Use modulo to map byte to valid character index
                    int charIndex = randomBytes[i] % ValidCharsCount;
                    randomPart.Append(ValidChars[charIndex]);
                }
                
                string randomString = randomPart.ToString();
                char checksum = CalculateChecksum(randomString);
                
                return new SessionCode(randomString + checksum);
            }
        }

        /// <summary>
        /// Validates a session code string without creating an instance.
        /// </summary>
        public static bool IsValid(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != TotalLength)
                return false;
            
            code = code.ToUpperInvariant();
            
            foreach (char c in code)
            {
                if (!ValidChars.Contains(c))
                    return false;
            }
            
            return ValidateChecksum(code);
        }

        /// <summary>
        /// Gets the session code as a string.
        /// </summary>
        public override string ToString() => _code;

        /// <summary>
        /// Gets the display-friendly version of the code (with hyphen for readability).
        /// Format: XXXX-XXXX (4 characters, hyphen, 4 characters)
        /// </summary>
        public string ToDisplayString()
        {
            return $"{_code.Substring(0, 4)}-{_code.Substring(4, 5)}";
        }

        /// <summary>
        /// Gets the random part of the code (first 8 characters).
        /// </summary>
        public string RandomPart => _code.Substring(0, RandomLength);

        /// <summary>
        /// Gets the checksum character (last character).
        /// </summary>
        public char Checksum => _code[RandomLength];

        /// <summary>
        /// Equality comparison based on the code string.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is SessionCode other && _code == other._code;
        }

        /// <summary>
        /// Hash code based on the code string.
        /// </summary>
        public override int GetHashCode() => _code.GetHashCode();

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(SessionCode left, SessionCode right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(SessionCode left, SessionCode right) => !(left == right);

        /// <summary>
        /// Implicit conversion to string for convenience.
        /// </summary>
        public static implicit operator string(SessionCode code) => code?._code;

        private static char CalculateChecksum(string randomPart)
        {
            // Improved checksum: weighted sum with prime multiplier for better error detection
            // This is similar to Luhn algorithm but adapted for our character set
            int sum = 0;
            int weight = 3; // Prime number for better distribution
            
            for (int i = 0; i < randomPart.Length; i++)
            {
                int charValue = ValidChars.IndexOf(randomPart[i]);
                // Alternate weights: 3, 7 (both prime)
                int currentWeight = (i % 2 == 0) ? weight : 7;
                sum += charValue * currentWeight;
            }
            
            // Use modulo with ValidCharsCount to get checksum character
            return ValidChars[sum % ValidCharsCount];
        }

        private static bool ValidateChecksum(string code)
        {
            string randomPart = code.Substring(0, RandomLength);
            char expectedChecksum = CalculateChecksum(randomPart);
            char actualChecksum = code[RandomLength];
            
            return expectedChecksum == actualChecksum;
        }
    }
}