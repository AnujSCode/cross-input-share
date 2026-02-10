using System;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Core.Interfaces
{
    /// <summary>
    /// Represents a connection to a remote device.
    /// Handles sending and receiving input events and screen data.
    /// </summary>
    public interface IDeviceConnection : IDisposable
    {
        /// <summary>
        /// Event raised when the connection is disconnected.
        /// </summary>
        event EventHandler Disconnected;
        
        /// <summary>
        /// Event raised when a keyboard event is received.
        /// </summary>
        event EventHandler<KeyboardEvent> KeyboardEventReceived;
        
        /// <summary>
        /// Event raised when a mouse event is received.
        /// </summary>
        event EventHandler<MouseEvent> MouseEventReceived;
        
        /// <summary>
        /// Event raised when clipboard data is received.
        /// </summary>
        event EventHandler<ClipboardData> ClipboardDataReceived;
        
        /// <summary>
        /// Event raised when screen data is received.
        /// </summary>
        event EventHandler<ScreenData> ScreenDataReceived;
        
        /// <summary>
        /// Gets the ID of the remote device.
        /// </summary>
        Guid RemoteDeviceId { get; }
        
        /// <summary>
        /// Gets whether the connection is currently active.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Gets the connection latency in milliseconds.
        /// </summary>
        int LatencyMs { get; }
        
        /// <summary>
        /// Gets the connection quality (0.0 to 1.0).
        /// </summary>
        double Quality { get; }

        /// <summary>
        /// Sends a keyboard event to the remote device.
        /// </summary>
        /// <param name="keyboardEvent">The keyboard event to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendKeyboardEventAsync(KeyboardEvent keyboardEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a mouse event to the remote device.
        /// </summary>
        /// <param name="mouseEvent">The mouse event to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendMouseEventAsync(MouseEvent mouseEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends clipboard data to the remote device.
        /// </summary>
        /// <param name="clipboardData">The clipboard data to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendClipboardDataAsync(ClipboardData clipboardData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends screen data to the remote device.
        /// </summary>
        /// <param name="screenData">The screen data to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendScreenDataAsync(ScreenData screenData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pings the remote device to measure latency.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The round-trip latency in milliseconds</returns>
        Task<int> PingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the connection gracefully.
        /// </summary>
        /// <param name="reason">Reason for closing</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CloseAsync(string reason = "Normal closure", CancellationToken cancellationToken = default);
    }
}