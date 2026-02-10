using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrossInputShare.Core.Models;
using System;

namespace CrossInputShare.UI.ViewModels
{
    /// <summary>
    /// ViewModel for a connected device
    /// </summary>
    public partial class DeviceViewModel : ViewModelBase
    {
        [ObservableProperty]
        private Guid _id = Guid.NewGuid();

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _platform = string.Empty;

        [ObservableProperty]
        private DeviceRole _role = DeviceRole.Client;

        [ObservableProperty]
        private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;

        [ObservableProperty]
        private bool _isLocalDevice;

        [ObservableProperty]
        private bool _isKeyboardShared;

        [ObservableProperty]
        private bool _isMouseShared;

        [ObservableProperty]
        private bool _isClipboardShared;

        [ObservableProperty]
        private bool _isScreenShared;

        [ObservableProperty]
        private string _fingerprint = string.Empty;

        [ObservableProperty]
        private DateTime _connectedSince = DateTime.Now;

        /// <summary>
        /// Gets the display name for the device
        /// </summary>
        public string DisplayName => IsLocalDevice ? $"{Name} (This Device)" : Name;

        /// <summary>
        /// Gets the connection status color for UI display
        /// </summary>
        public string StatusColor
        {
            get
            {
                return ConnectionStatus switch
                {
                    ConnectionStatus.Connected => "#4CAF50", // Green
                    ConnectionStatus.Connecting => "#FFC107", // Yellow/Amber
                    ConnectionStatus.Disconnected => "#F44336", // Red
                    ConnectionStatus.Error => "#9C27B0", // Purple
                    _ => "#757575" // Gray
                };
            }
        }

        /// <summary>
        /// Gets the role badge color for UI display
        /// </summary>
        public string RoleColor
        {
            get
            {
                return Role switch
                {
                    DeviceRole.Server => "#2196F3", // Blue
                    DeviceRole.Client => "#4CAF50", // Green
                    DeviceRole.Auto => "#FF9800", // Orange
                    _ => "#757575" // Gray
                };
            }
        }

        /// <summary>
        /// Gets the role display text
        /// </summary>
        public string RoleDisplay => Role.GetDisplayName();

        /// <summary>
        /// Gets the connection status display text
        /// </summary>
        public string StatusDisplay
        {
            get
            {
                return ConnectionStatus switch
                {
                    ConnectionStatus.Connected => "Connected",
                    ConnectionStatus.Connecting => "Connecting...",
                    ConnectionStatus.Disconnected => "Disconnected",
                    ConnectionStatus.Error => "Error",
                    _ => "Unknown"
                };
            }
        }

        /// <summary>
        /// Gets a short fingerprint for display
        /// </summary>
        public string ShortFingerprint => Fingerprint.Length > 12 ? 
            $"{Fingerprint[..6]}...{Fingerprint[^6..]}" : Fingerprint;

        [RelayCommand]
        private void ToggleKeyboard()
        {
            IsKeyboardShared = !IsKeyboardShared;
        }

        [RelayCommand]
        private void ToggleMouse()
        {
            IsMouseShared = !IsMouseShared;
        }

        [RelayCommand]
        private void ToggleClipboard()
        {
            IsClipboardShared = !IsClipboardShared;
        }

        [RelayCommand]
        private void ToggleScreen()
        {
            IsScreenShared = !IsScreenShared;
        }

        [RelayCommand]
        private void DisconnectDevice()
        {
            ConnectionStatus = ConnectionStatus.Disconnected;
        }

        [RelayCommand]
        private void VerifyDevice()
        {
            // TODO: Implement device verification logic
            // This would open a verification dialog comparing fingerprints
        }

        /// <summary>
        /// Updates the device from a DeviceInfo model
        /// </summary>
        public void UpdateFromModel(DeviceInfo deviceInfo)
        {
            Id = deviceInfo.Id;
            Name = deviceInfo.Name;
            Platform = deviceInfo.Platform;
            Fingerprint = deviceInfo.Fingerprint?.ToString() ?? string.Empty;
            ConnectedSince = deviceInfo.JoinedAt;
        }

        /// <summary>
        /// Creates a DeviceViewModel from a DeviceInfo model
        /// </summary>
        public static DeviceViewModel FromModel(DeviceInfo deviceInfo, bool isLocalDevice = false)
        {
            return new DeviceViewModel
            {
                Id = deviceInfo.Id,
                Name = deviceInfo.Name,
                Platform = deviceInfo.Platform,
                Fingerprint = deviceInfo.Fingerprint?.ToString() ?? string.Empty,
                ConnectedSince = deviceInfo.JoinedAt,
                IsLocalDevice = isLocalDevice,
                ConnectionStatus = ConnectionStatus.Connected,
                Role = deviceInfo.IsHost ? DeviceRole.Server : DeviceRole.Client
            };
        }
    }
}