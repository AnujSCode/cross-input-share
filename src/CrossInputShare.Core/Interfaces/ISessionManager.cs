using System;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Core.Interfaces
{
    /// <summary>
    /// Manages session creation, validation, and lifecycle.
    /// Handles session codes, device authentication, and manual verification.
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Event raised when a new session is created.
        /// </summary>
        event EventHandler<SessionCreatedEventArgs> SessionCreated;
        
        /// <summary>
        /// Event raised when a session is verified (manual verification completed).
        /// </summary>
        event EventHandler<SessionVerifiedEventArgs> SessionVerified;
        
        /// <summary>
        /// Event raised when a session expires or is terminated.
        /// </summary>
        event EventHandler<SessionEndedEventArgs> SessionEnded;

        /// <summary>
        /// Creates a new session with the specified timeout.
        /// </summary>
        /// <param name="timeout">Optional session timeout. If null, uses default timeout.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>The created session information</returns>
        Task<SessionInfo> CreateSessionAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Joins an existing session using a session code.
        /// </summary>
        /// <param name="sessionCode">The session code to join</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>The session information if successful</returns>
        /// <exception cref="ArgumentException">Thrown if session code is invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown if session doesn't exist or is full</exception>
        Task<SessionInfo> JoinSessionAsync(SessionCode sessionCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a session manually by comparing device fingerprints.
        /// This is the manual verification step where users confirm they see the same fingerprints.
        /// </summary>
        /// <param name="sessionId">The session ID to verify</param>
        /// <param name="remoteFingerprint">The fingerprint of the remote device to verify</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>True if verification was successful</returns>
        Task<bool> VerifySessionAsync(Guid sessionId, DeviceFingerprint remoteFingerprint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current active session, if any.
        /// </summary>
        /// <returns>The current session or null if no active session</returns>
        SessionInfo GetCurrentSession();

        /// <summary>
        /// Ends the current session gracefully.
        /// </summary>
        /// <param name="reason">Reason for ending the session</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        Task EndSessionAsync(string reason = "User requested", CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a session code is syntactically correct.
        /// Does not check if the session actually exists.
        /// </summary>
        /// <param name="code">The code to validate</param>
        /// <returns>True if the code format is valid</returns>
        bool ValidateSessionCode(string code);
    }

    /// <summary>
    /// Event arguments for session creation events.
    /// </summary>
    public class SessionCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// The session that was created.
        /// </summary>
        public SessionInfo Session { get; }

        /// <summary>
        /// Initializes a new instance of the SessionCreatedEventArgs class.
        /// </summary>
        /// <param name="session">The session that was created</param>
        public SessionCreatedEventArgs(SessionInfo session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }
    }

    /// <summary>
    /// Event arguments for session verification events.
    /// </summary>
    public class SessionVerifiedEventArgs : EventArgs
    {
        /// <summary>
        /// The session that was verified.
        /// </summary>
        public SessionInfo Session { get; }

        /// <summary>
        /// The fingerprint of the remote device that was verified.
        /// </summary>
        public DeviceFingerprint RemoteFingerprint { get; }

        /// <summary>
        /// Initializes a new instance of the SessionVerifiedEventArgs class.
        /// </summary>
        /// <param name="session">The session that was verified</param>
        /// <param name="remoteFingerprint">The fingerprint of the remote device</param>
        public SessionVerifiedEventArgs(SessionInfo session, DeviceFingerprint remoteFingerprint)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            RemoteFingerprint = remoteFingerprint ?? throw new ArgumentNullException(nameof(remoteFingerprint));
        }
    }

    /// <summary>
    /// Event arguments for session ending events.
    /// </summary>
    public class SessionEndedEventArgs : EventArgs
    {
        /// <summary>
        /// The session that ended.
        /// </summary>
        public SessionInfo Session { get; }

        /// <summary>
        /// The reason the session ended.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the SessionEndedEventArgs class.
        /// </summary>
        /// <param name="session">The session that ended</param>
        /// <param name="reason">The reason the session ended</param>
        public SessionEndedEventArgs(SessionInfo session, string reason)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }
    }
}