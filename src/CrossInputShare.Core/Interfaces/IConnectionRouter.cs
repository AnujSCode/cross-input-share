using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Core.Interfaces
{
    /// <summary>
    /// Manages connection routing in a star topology (server-client model).
    /// Routes input from server to clients and screens between any devices.
    /// </summary>
    public interface IConnectionRouter : IDisposable
    {
        /// <summary>
        /// Event raised when a device connects.
        /// </summary>
        event EventHandler<DeviceConnectedEventArgs> DeviceConnected;
        
        /// <summary>
        /// Event raised when a device disconnects.
        /// </summary>
        event EventHandler<DeviceDisconnectedEventArgs> DeviceDisconnected;
        
        /// <summary>
        /// Event raised when screen sharing starts or stops.
        /// </summary>
        event EventHandler<ScreenSharingEventArgs> ScreenSharingChanged;

        /// <summary>
        /// Adds a device connection to the router.
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="connection">The connection to the device</param>
        /// <param name="role">The role of the device (Server/Client)</param>
        void AddConnection(Guid deviceId, IDeviceConnection connection, DeviceRole role);

        /// <summary>
        /// Routes keyboard input from the server to all connected clients.
        /// </summary>
        /// <param name="keyboardEvent">The keyboard event to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RouteKeyboardInputAsync(KeyboardEvent keyboardEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Routes mouse input from the server to all connected clients.
        /// </summary>
        /// <param name="mouseEvent">The mouse event to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RouteMouseInputAsync(MouseEvent mouseEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Routes clipboard data from the server to all connected clients.
        /// </summary>
        /// <param name="clipboardData">The clipboard data to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RouteClipboardAsync(ClipboardData clipboardData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Routes screen data from any device to specified destinations.
        /// Implements any-to-any screen sharing model.
        /// </summary>
        /// <param name="screenData">The screen data to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RouteScreenDataAsync(ScreenData screenData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Configures screen sharing routes for a device.
        /// </summary>
        /// <param name="sourceDeviceId">The device that will share its screen</param>
        /// <param name="destinationDeviceIds">Devices that will receive the screen</param>
        void ConfigureScreenSharing(Guid sourceDeviceId, IEnumerable<Guid> destinationDeviceIds);

        /// <summary>
        /// Starts screen sharing from a device to one or more destinations.
        /// </summary>
        /// <param name="sourceDeviceId">The device that will share its screen</param>
        /// <param name="destinationDeviceIds">Devices that will receive the screen</param>
        void StartScreenSharing(Guid sourceDeviceId, params Guid[] destinationDeviceIds);

        /// <summary>
        /// Stops screen sharing from a device.
        /// </summary>
        /// <param name="sourceDeviceId">The device that is sharing its screen</param>
        void StopScreenSharing(Guid sourceDeviceId);

        /// <summary>
        /// Gets the current screen sharing configuration for a device.
        /// </summary>
        /// <param name="sourceDeviceId">The device to check</param>
        /// <returns>List of destination device IDs</returns>
        IReadOnlyList<Guid> GetScreenSharingDestinations(Guid sourceDeviceId);

        /// <summary>
        /// Removes a device connection from the router.
        /// </summary>
        /// <param name="deviceId">The device ID to remove</param>
        void RemoveConnection(Guid deviceId);

        /// <summary>
        /// Gets all connected device IDs.
        /// </summary>
        IReadOnlyList<Guid> GetConnectedDeviceIds();

        /// <summary>
        /// Gets the role of a connected device.
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <returns>The device role</returns>
        DeviceRole GetDeviceRole(Guid deviceId);

        /// <summary>
        /// Gets the server device ID, if connected.
        /// </summary>
        /// <returns>The server device ID or null if no server is connected</returns>
        Guid? GetServerDeviceId();

        /// <summary>
        /// Gets all client device IDs.
        /// </summary>
        IReadOnlyList<Guid> GetClientDeviceIds();
    }
}