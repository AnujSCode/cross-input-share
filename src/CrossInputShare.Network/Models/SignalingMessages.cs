using System;
using System.Text.Json.Serialization;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Network.Models
{
    /// <summary>
    /// Base class for all signaling messages.
    /// </summary>
    public abstract class SignalingMessage
    {
        /// <summary>
        /// Message type identifier.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; }
        
        /// <summary>
        /// Unique message identifier.
        /// </summary>
        [JsonPropertyName("messageId")]
        public Guid MessageId { get; }
        
        /// <summary>
        /// Timestamp when the message was created (UTC).
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; }

        protected SignalingMessage(string type)
        {
            Type = type;
            MessageId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Session creation request.
    /// </summary>
    public class CreateSessionRequest : SignalingMessage
    {
        /// <summary>
        /// Device information for the session creator.
        /// </summary>
        [JsonPropertyName("deviceInfo")]
        public DeviceInfo DeviceInfo { get; }
        
        /// <summary>
        /// Session features and capabilities.
        /// </summary>
        [JsonPropertyName("features")]
        public SessionFeatures Features { get; }

        public CreateSessionRequest(DeviceInfo deviceInfo, SessionFeatures features)
            : base("create-session")
        {
            DeviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
            Features = features ?? throw new ArgumentNullException(nameof(features));
        }
    }

    /// <summary>
    /// Session creation response.
    /// </summary>
    public class CreateSessionResponse : SignalingMessage
    {
        /// <summary>
        /// Session code for joining.
        /// </summary>
        [JsonPropertyName("sessionCode")]
        public string SessionCode { get; }
        
        /// <summary>
        /// Session information.
        /// </summary>
        [JsonPropertyName("sessionInfo")]
        public SessionInfo SessionInfo { get; }
        
        /// <summary>
        /// Whether the session was created successfully.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; }
        
        /// <summary>
        /// Error message if creation failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; }

        public CreateSessionResponse(string sessionCode, SessionInfo sessionInfo, bool success, string? error = null)
            : base("create-session-response")
        {
            SessionCode = sessionCode ?? throw new ArgumentNullException(nameof(sessionCode));
            SessionInfo = sessionInfo ?? throw new ArgumentNullException(nameof(sessionInfo));
            Success = success;
            Error = error;
        }
    }

    /// <summary>
    /// Join session request.
    /// </summary>
    public class JoinSessionRequest : SignalingMessage
    {
        /// <summary>
        /// Session code to join.
        /// </summary>
        [JsonPropertyName("sessionCode")]
        public string SessionCode { get; }
        
        /// <summary>
        /// Device information for the joining device.
        /// </summary>
        [JsonPropertyName("deviceInfo")]
        public DeviceInfo DeviceInfo { get; }

        public JoinSessionRequest(string sessionCode, DeviceInfo deviceInfo)
            : base("join-session")
        {
            SessionCode = sessionCode ?? throw new ArgumentNullException(nameof(sessionCode));
            DeviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        }
    }

    /// <summary>
    /// Join session response.
    /// </summary>
    public class JoinSessionResponse : SignalingMessage
    {
        /// <summary>
        /// Whether the join was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; }
        
        /// <summary>
        /// Session information if join was successful.
        /// </summary>
        [JsonPropertyName("sessionInfo")]
        public SessionInfo? SessionInfo { get; }
        
        /// <summary>
        /// Error message if join failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; }

        public JoinSessionResponse(bool success, SessionInfo? sessionInfo = null, string? error = null)
            : base("join-session-response")
        {
            Success = success;
            SessionInfo = sessionInfo;
            Error = error;
        }
    }

    /// <summary>
    /// WebRTC offer message.
    /// </summary>
    public class WebRtcOffer : SignalingMessage
    {
        /// <summary>
        /// Session description protocol (SDP) offer.
        /// </summary>
        [JsonPropertyName("sdp")]
        public string Sdp { get; }
        
        /// <summary>
        /// Target device ID for the connection.
        /// </summary>
        [JsonPropertyName("targetDeviceId")]
        public Guid TargetDeviceId { get; }
        
        /// <summary>
        /// Source device ID initiating the connection.
        /// </summary>
        [JsonPropertyName("sourceDeviceId")]
        public Guid SourceDeviceId { get; }

        public WebRtcOffer(string sdp, Guid sourceDeviceId, Guid targetDeviceId)
            : base("webrtc-offer")
        {
            Sdp = sdp ?? throw new ArgumentNullException(nameof(sdp));
            SourceDeviceId = sourceDeviceId;
            TargetDeviceId = targetDeviceId;
        }
    }

    /// <summary>
    /// WebRTC answer message.
    /// </summary>
    public class WebRtcAnswer : SignalingMessage
    {
        /// <summary>
        /// Session description protocol (SDP) answer.
        /// </summary>
        [JsonPropertyName("sdp")]
        public string Sdp { get; }
        
        /// <summary>
        /// Target device ID for the connection.
        /// </summary>
        [JsonPropertyName("targetDeviceId")]
        public Guid TargetDeviceId { get; }
        
        /// <summary>
        /// Source device ID responding to the offer.
        /// </summary>
        [JsonPropertyName("sourceDeviceId")]
        public Guid SourceDeviceId { get; }

        public WebRtcAnswer(string sdp, Guid sourceDeviceId, Guid targetDeviceId)
            : base("webrtc-answer")
        {
            Sdp = sdp ?? throw new ArgumentNullException(nameof(sdp));
            SourceDeviceId = sourceDeviceId;
            TargetDeviceId = targetDeviceId;
        }
    }

    /// <summary>
    /// ICE candidate message.
    /// </summary>
    public class IceCandidate : SignalingMessage
    {
        /// <summary>
        /// ICE candidate string.
        /// </summary>
        [JsonPropertyName("candidate")]
        public string Candidate { get; }
        
        /// <summary>
        /// SDP media stream identification.
        /// </summary>
        [JsonPropertyName("sdpMid")]
        public string? SdpMid { get; }
        
        /// <summary>
        /// SDP media stream index.
        /// </summary>
        [JsonPropertyName("sdpMLineIndex")]
        public int? SdpMLineIndex { get; }
        
        /// <summary>
        /// Target device ID for the candidate.
        /// </summary>
        [JsonPropertyName("targetDeviceId")]
        public Guid TargetDeviceId { get; }
        
        /// <summary>
        /// Source device ID sending the candidate.
        /// </summary>
        [JsonPropertyName("sourceDeviceId")]
        public Guid SourceDeviceId { get; }

        public IceCandidate(string candidate, Guid sourceDeviceId, Guid targetDeviceId, string? sdpMid = null, int? sdpMLineIndex = null)
            : base("ice-candidate")
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            SourceDeviceId = sourceDeviceId;
            TargetDeviceId = targetDeviceId;
            SdpMid = sdpMid;
            SdpMLineIndex = sdpMLineIndex;
        }
    }

    /// <summary>
    /// Session information update.
    /// </summary>
    public class SessionInfoUpdate : SignalingMessage
    {
        /// <summary>
        /// Updated session information.
        /// </summary>
        [JsonPropertyName("sessionInfo")]
        public SessionInfo SessionInfo { get; }

        public SessionInfoUpdate(SessionInfo sessionInfo)
            : base("session-info-update")
        {
            SessionInfo = sessionInfo ?? throw new ArgumentNullException(nameof(sessionInfo));
        }
    }

    /// <summary>
    /// Device connected notification.
    /// </summary>
    public class DeviceConnectedNotification : SignalingMessage
    {
        /// <summary>
        /// Connected device information.
        /// </summary>
        [JsonPropertyName("deviceInfo")]
        public DeviceInfo DeviceInfo { get; }
        
        /// <summary>
        /// Device role (Server/Client).
        /// </summary>
        [JsonPropertyName("role")]
        public DeviceRole Role { get; }

        public DeviceConnectedNotification(DeviceInfo deviceInfo, DeviceRole role)
            : base("device-connected")
        {
            DeviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
            Role = role;
        }
    }

    /// <summary>
    /// Device disconnected notification.
    /// </summary>
    public class DeviceDisconnectedNotification : SignalingMessage
    {
        /// <summary>
        /// ID of the disconnected device.
        /// </summary>
        [JsonPropertyName("deviceId")]
        public Guid DeviceId { get; }
        
        /// <summary>
        /// Reason for disconnection.
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; }

        public DeviceDisconnectedNotification(Guid deviceId, string reason)
            : base("device-disconnected")
        {
            DeviceId = deviceId;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }
    }

    /// <summary>
    /// Ping message for latency measurement.
    /// </summary>
    public class PingMessage : SignalingMessage
    {
        /// <summary>
        /// Sequence number for tracking.
        /// </summary>
        [JsonPropertyName("sequence")]
        public long Sequence { get; }

        public PingMessage(long sequence)
            : base("ping")
        {
            Sequence = sequence;
        }
    }

    /// <summary>
    /// Pong message for latency measurement.
    /// </summary>
    public class PongMessage : SignalingMessage
    {
        /// <summary>
        /// Sequence number from the ping.
        /// </summary>
        [JsonPropertyName("sequence")]
        public long Sequence { get; }
        
        /// <summary>
        /// Timestamp when the ping was received (UTC).
        /// </summary>
        [JsonPropertyName("pingTimestamp")]
        public DateTime PingTimestamp { get; }

        public PongMessage(long sequence, DateTime pingTimestamp)
            : base("pong")
        {
            Sequence = sequence;
            PingTimestamp = pingTimestamp;
        }
    }

    /// <summary>
    /// Error message.
    /// </summary>
    public class ErrorMessage : SignalingMessage
    {
        /// <summary>
        /// Error code.
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; }
        
        /// <summary>
        /// Error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; }
        
        /// <summary>
        /// Optional details about the error.
        /// </summary>
        [JsonPropertyName("details")]
        public object? Details { get; }

        public ErrorMessage(string code, string message, object? details = null)
            : base("error")
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Details = details;
        }
    }

    /// <summary>
    /// Screen sharing configuration message.
    /// </summary>
    public class ScreenSharingConfig : SignalingMessage
    {
        /// <summary>
        /// Source device ID sharing its screen.
        /// </summary>
        [JsonPropertyName("sourceDeviceId")]
        public Guid SourceDeviceId { get; }
        
        /// <summary>
        /// Destination device IDs to receive the screen.
        /// </summary>
        [JsonPropertyName("destinationDeviceIds")]
        public Guid[] DestinationDeviceIds { get; }
        
        /// <summary>
        /// Action to perform (start/stop/configure).
        /// </summary>
        [JsonPropertyName("action")]
        public string Action { get; }

        public ScreenSharingConfig(Guid sourceDeviceId, Guid[] destinationDeviceIds, string action)
            : base("screen-sharing-config")
        {
            SourceDeviceId = sourceDeviceId;
            DestinationDeviceIds = destinationDeviceIds ?? throw new ArgumentNullException(nameof(destinationDeviceIds));
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }
    }
}