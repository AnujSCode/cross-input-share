using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Interfaces;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Network.Services
{
    /// <summary>
    /// Manages connection routing in a star topology (server-client model).
    /// Routes input from server to clients and screens between any devices.
    /// Implements the any-to-any screen sharing model.
    /// </summary>
    public class ConnectionRouter : IConnectionRouter, IDisposable
    {
        private readonly Dictionary<Guid, IDeviceConnection> _connections = new();
        private readonly Dictionary<Guid, DeviceRole> _deviceRoles = new();
        private readonly Dictionary<Guid, List<Guid>> _screenSharingRoutes = new();
        private readonly object _lock = new object();
        private bool _disposed = false;
        
        /// <summary>
        /// Event raised when a device connects.
        /// </summary>
        public event EventHandler<DeviceConnectedEventArgs> DeviceConnected;
        
        /// <summary>
        /// Event raised when a device disconnects.
        /// </summary>
        public event EventHandler<DeviceDisconnectedEventArgs> DeviceDisconnected;
        
        /// <summary>
        /// Event raised when screen sharing starts or stops.
        /// </summary>
        public event EventHandler<ScreenSharingEventArgs> ScreenSharingChanged;

        /// <summary>
        /// Adds a device connection to the router.
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="connection">The connection to the device</param>
        /// <param name="role">The role of the device (Server/Client)</param>
        public void AddConnection(Guid deviceId, IDeviceConnection connection, DeviceRole role)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_connections.ContainsKey(deviceId))
                    throw new InvalidOperationException($"Device {deviceId} is already connected");
                
                _connections[deviceId] = connection ?? throw new ArgumentNullException(nameof(connection));
                _deviceRoles[deviceId] = role;
                _screenSharingRoutes[deviceId] = new List<Guid>();
                
                // Subscribe to connection events
                connection.Disconnected += (s, e) => HandleDeviceDisconnected(deviceId);
                
                DeviceConnected?.Invoke(this, new DeviceConnectedEventArgs(deviceId, role));
            }
        }

        /// <summary>
        /// Routes keyboard input from the server to all connected clients.
        /// </summary>
        /// <param name="keyboardEvent">The keyboard event to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the routing operation</returns>
        public async Task RouteKeyboardInputAsync(KeyboardEvent keyboardEvent, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var serverId = GetServerDeviceId();
            if (serverId == null)
                throw new InvalidOperationException("No server device found");
            
            // Only server can send keyboard input
            if (keyboardEvent.SourceDeviceId != serverId.Value)
                throw new InvalidOperationException("Only server device can send keyboard input");
            
            // Route to all clients
            var clientIds = GetClientDeviceIds();
            var tasks = new List<Task>();
            
            foreach (var clientId in clientIds)
            {
                if (_connections.TryGetValue(clientId, out var connection))
                {
                    tasks.Add(connection.SendKeyboardEventAsync(keyboardEvent, cancellationToken));
                }
            }
            
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Routes mouse input from the server to all connected clients.
        /// </summary>
        /// <param name="mouseEvent">The mouse event to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the routing operation</returns>
        public async Task RouteMouseInputAsync(MouseEvent mouseEvent, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var serverId = GetServerDeviceId();
            if (serverId == null)
                throw new InvalidOperationException("No server device found");
            
            // Only server can send mouse input
            if (mouseEvent.SourceDeviceId != serverId.Value)
                throw new InvalidOperationException("Only server device can send mouse input");
            
            // Route to all clients
            var clientIds = GetClientDeviceIds();
            var tasks = new List<Task>();
            
            foreach (var clientId in clientIds)
            {
                if (_connections.TryGetValue(clientId, out var connection))
                {
                    tasks.Add(connection.SendMouseEventAsync(mouseEvent, cancellationToken));
                }
            }
            
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Routes clipboard data from the server to all connected clients.
        /// </summary>
        /// <param name="clipboardData">The clipboard data to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the routing operation</returns>
        public async Task RouteClipboardAsync(ClipboardData clipboardData, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var serverId = GetServerDeviceId();
            if (serverId == null)
                throw new InvalidOperationException("No server device found");
            
            // Only server can send clipboard data (clients can send back if enabled)
            if (clipboardData.SourceDeviceId != serverId.Value)
            {
                // Check if client-to-server clipboard is enabled
                if (!IsClientToServerClipboardEnabled())
                    throw new InvalidOperationException("Client-to-server clipboard is not enabled");
            }
            
            // Determine destination based on source
            IEnumerable<Guid> destinationIds;
            if (clipboardData.SourceDeviceId == serverId.Value)
            {
                // Server to all clients
                destinationIds = GetClientDeviceIds();
            }
            else
            {
                // Client to server only
                destinationIds = new[] { serverId.Value };
            }
            
            var tasks = new List<Task>();
            foreach (var destId in destinationIds)
            {
                if (_connections.TryGetValue(destId, out var connection))
                {
                    tasks.Add(connection.SendClipboardDataAsync(clipboardData, cancellationToken));
                }
            }
            
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Routes screen data from any device to specified destinations.
        /// Implements any-to-any screen sharing model.
        /// </summary>
        /// <param name="screenData">The screen data to route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the routing operation</returns>
        public async Task RouteScreenDataAsync(ScreenData screenData, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (!_connections.ContainsKey(screenData.SourceDeviceId))
                    throw new InvalidOperationException($"Source device {screenData.SourceDeviceId} is not connected");
                
                // Get destinations for this source device
                if (!_screenSharingRoutes.TryGetValue(screenData.SourceDeviceId, out var destinationIds))
                    throw new InvalidOperationException($"No screen sharing routes configured for device {screenData.SourceDeviceId}");
                
                if (destinationIds.Count == 0)
                    return; // No destinations configured
                
                var tasks = new List<Task>();
                foreach (var destId in destinationIds)
                {
                    if (_connections.TryGetValue(destId, out var connection))
                    {
                        tasks.Add(connection.SendScreenDataAsync(screenData, cancellationToken));
                    }
                }
                
                Task.Run(async () => await Task.WhenAll(tasks), cancellationToken);
            }
        }

        /// <summary>
        /// Configures screen sharing routes for a device.
        /// </summary>
        /// <param name="sourceDeviceId">The device that will share its screen</param>
        /// <param name="destinationDeviceIds">Devices that will receive the screen</param>
        public void ConfigureScreenSharing(Guid sourceDeviceId, IEnumerable<Guid> destinationDeviceIds)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (!_connections.ContainsKey(sourceDeviceId))
                    throw new InvalidOperationException($"Source device {sourceDeviceId} is not connected");
                
                var validDestinations = new List<Guid>();
                foreach (var destId in destinationDeviceIds)
                {
                    if (_connections.ContainsKey(destId) && destId != sourceDeviceId)
                    {
                        validDestinations.Add(destId);
                    }
                }
                
                _screenSharingRoutes[sourceDeviceId] = validDestinations;
                
                ScreenSharingChanged?.Invoke(this, new ScreenSharingEventArgs(
                    sourceDeviceId, 
                    validDestinations, 
                    ScreenSharingAction.Configured
                ));
            }
        }

        /// <summary>
        /// Starts screen sharing from a device to one or more destinations.
        /// </summary>
        /// <param name="sourceDeviceId">The device that will share its screen</param>
        /// <param name="destinationDeviceIds">Devices that will receive the screen</param>
        public void StartScreenSharing(Guid sourceDeviceId, params Guid[] destinationDeviceIds)
        {
            ConfigureScreenSharing(sourceDeviceId, destinationDeviceIds);
            
            ScreenSharingChanged?.Invoke(this, new ScreenSharingEventArgs(
                sourceDeviceId, 
                destinationDeviceIds, 
                ScreenSharingAction.Started
            ));
        }

        /// <summary>
        /// Stops screen sharing from a device.
        /// </summary>
        /// <param name="sourceDeviceId">The device that is sharing its screen</param>
        public void StopScreenSharing(Guid sourceDeviceId)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_screenSharingRoutes.ContainsKey(sourceDeviceId))
                {
                    _screenSharingRoutes[sourceDeviceId].Clear();
                    
                    ScreenSharingChanged?.Invoke(this, new ScreenSharingEventArgs(
                        sourceDeviceId, 
                        Array.Empty<Guid>(), 
                        ScreenSharingAction.Stopped
                    ));
                }
            }
        }

        /// <summary>
        /// Gets the current screen sharing configuration for a device.
        /// </summary>
        /// <param name="sourceDeviceId">The device to check</param>
        /// <returns>List of destination device IDs</returns>
        public IReadOnlyList<Guid> GetScreenSharingDestinations(Guid sourceDeviceId)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_screenSharingRoutes.TryGetValue(sourceDeviceId, out var destinations))
                {
                    return destinations.AsReadOnly();
                }
                return Array.Empty<Guid>();
            }
        }

        /// <summary>
        /// Removes a device connection from the router.
        /// </summary>
        /// <param name="deviceId">The device ID to remove</param>
        public void RemoveConnection(Guid deviceId)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_connections.Remove(deviceId))
                {
                    _deviceRoles.Remove(deviceId);
                    _screenSharingRoutes.Remove(deviceId);
                    
                    // Remove this device from other devices' screen sharing routes
                    foreach (var routes in _screenSharingRoutes.Values)
                    {
                        routes.Remove(deviceId);
                    }
                    
                    DeviceDisconnected?.Invoke(this, new DeviceDisconnectedEventArgs(deviceId));
                }
            }
        }

        /// <summary>
        /// Gets all connected device IDs.
        /// </summary>
        public IReadOnlyList<Guid> GetConnectedDeviceIds()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                return _connections.Keys.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the role of a connected device.
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <returns>The device role</returns>
        public DeviceRole GetDeviceRole(Guid deviceId)
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                if (_deviceRoles.TryGetValue(deviceId, out var role))
                {
                    return role;
                }
                throw new InvalidOperationException($"Device {deviceId} is not connected");
            }
        }

        /// <summary>
        /// Gets the server device ID, if connected.
        /// </summary>
        /// <returns>The server device ID or null if no server is connected</returns>
        public Guid? GetServerDeviceId()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                foreach (var kvp in _deviceRoles)
                {
                    if (kvp.Value == DeviceRole.Server)
                    {
                        return kvp.Key;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets all client device IDs.
        /// </summary>
        public IReadOnlyList<Guid> GetClientDeviceIds()
        {
            ThrowIfDisposed();
            
            lock (_lock)
            {
                var clientIds = new List<Guid>();
                foreach (var kvp in _deviceRoles)
                {
                    if (kvp.Value == DeviceRole.Client)
                    {
                        clientIds.Add(kvp.Key);
                    }
                }
                return clientIds.AsReadOnly();
            }
        }

        /// <summary>
        /// Checks if client-to-server clipboard sharing is enabled.
        /// This would be configured in session settings.
        /// </summary>
        private bool IsClientToServerClipboardEnabled()
        {
            // In a real implementation, this would check session settings
            // For now, return true to allow bidirectional clipboard
            return true;
        }

        private void HandleDeviceDisconnected(Guid deviceId)
        {
            RemoveConnection(deviceId);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lock)
                {
                    foreach (var connection in _connections.Values)
                    {
                        connection.Dispose();
                    }
                    _connections.Clear();
                    _deviceRoles.Clear();
                    _screenSharingRoutes.Clear();
                    _disposed = true;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConnectionRouter));
        }
    }

    /// <summary>
    /// Event arguments for device connection events.
    /// </summary>
    public class DeviceConnectedEventArgs : EventArgs
    {
        public Guid DeviceId { get; }
        public DeviceRole Role { get; }
        
        public DeviceConnectedEventArgs(Guid deviceId, DeviceRole role)
        {
            DeviceId = deviceId;
            Role = role;
        }
    }

    /// <summary>
    /// Event arguments for device disconnection events.
    /// </summary>
    public class DeviceDisconnectedEventArgs : EventArgs
    {
        public Guid DeviceId { get; }
        
        public DeviceDisconnectedEventArgs(Guid deviceId)
        {
            DeviceId = deviceId;
        }
    }

    /// <summary>
    /// Event arguments for screen sharing events.
    /// </summary>
    public class ScreenSharingEventArgs : EventArgs
    {
        public Guid SourceDeviceId { get; }
        public IReadOnlyList<Guid> DestinationDeviceIds { get; }
        public ScreenSharingAction Action { get; }
        
        public ScreenSharingEventArgs(Guid sourceDeviceId, IEnumerable<Guid> destinationDeviceIds, ScreenSharingAction action)
        {
            SourceDeviceId = sourceDeviceId;
            DestinationDeviceIds = destinationDeviceIds?.ToList().AsReadOnly() ?? Array.Empty<Guid>();
            Action = action;
        }
    }

    /// <summary>
    /// Screen sharing actions.
    /// </summary>
    public enum ScreenSharingAction
    {
        Started,
        Stopped,
        Configured
    }
}