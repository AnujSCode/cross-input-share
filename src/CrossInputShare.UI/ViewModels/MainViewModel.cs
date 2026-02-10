using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrossInputShare.Core.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CrossInputShare.UI.ViewModels
{
    /// <summary>
    /// Main ViewModel for the application
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;

        [ObservableProperty]
        private DeviceRole _selectedRole = DeviceRole.Auto;

        [ObservableProperty]
        private SessionFeatures _enabledFeatures = SessionFeaturesExtensions.Default;

        [ObservableProperty]
        private ObservableCollection<DeviceViewModel> _connectedDevices = new();

        [ObservableProperty]
        private DeviceViewModel? _selectedDevice;

        [ObservableProperty]
        private string _sessionCode = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private ObservableCollection<DeviceRole> _availableRoles = new()
        {
            DeviceRole.Server,
            DeviceRole.Client,
            DeviceRole.Auto
        };

        [ObservableProperty]
        private bool _isSessionActive;

        [ObservableProperty]
        private bool _isKeyboardShared = true;

        [ObservableProperty]
        private bool _isMouseShared = true;

        [ObservableProperty]
        private bool _isClipboardShared = true;

        [ObservableProperty]
        private bool _isScreenShared;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class
        /// </summary>
        public MainViewModel()
        {
            Title = "Cross Input Share";
            
            // Initialize with sample devices for UI development
            InitializeSampleDevices();
        }

        private void InitializeSampleDevices()
        {
            // Add sample devices for UI development
            ConnectedDevices.Add(new DeviceViewModel
            {
                Name = "My Desktop (Server)",
                Platform = "Windows 11",
                Role = DeviceRole.Server,
                ConnectionStatus = ConnectionStatus.Connected,
                IsLocalDevice = true,
                IsKeyboardShared = true,
                IsMouseShared = true,
                IsClipboardShared = true,
                IsScreenShared = false
            });

            ConnectedDevices.Add(new DeviceViewModel
            {
                Name = "Laptop (Client)",
                Platform = "Ubuntu 22.04",
                Role = DeviceRole.Client,
                ConnectionStatus = ConnectionStatus.Connected,
                IsKeyboardShared = true,
                IsMouseShared = true,
                IsClipboardShared = true,
                IsScreenShared = false
            });

            ConnectedDevices.Add(new DeviceViewModel
            {
                Name = "Tablet (Client)",
                Platform = "Android 14",
                Role = DeviceRole.Client,
                ConnectionStatus = ConnectionStatus.Connecting,
                IsKeyboardShared = false,
                IsMouseShared = true,
                IsClipboardShared = true,
                IsScreenShared = true
            });
        }

        [RelayCommand]
        private async Task CreateSession()
        {
            IsBusy = true;
            try
            {
                // TODO: Implement actual session creation logic
                SessionCode = "ABCD-1234";
                IsSessionActive = true;
                ConnectionStatus = ConnectionStatus.Connected;
                
                // Show notification
                await ShowNotification("Session Created", $"Session code: {SessionCode}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task JoinSession()
        {
            if (string.IsNullOrWhiteSpace(SessionCode))
            {
                await ShowNotification("Error", "Please enter a session code");
                return;
            }

            IsBusy = true;
            try
            {
                // TODO: Implement actual session joining logic
                IsSessionActive = true;
                ConnectionStatus = ConnectionStatus.Connected;
                
                await ShowNotification("Session Joined", $"Connected to session: {SessionCode}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Disconnect()
        {
            IsSessionActive = false;
            ConnectionStatus = ConnectionStatus.Disconnected;
            SessionCode = string.Empty;
        }

        [RelayCommand]
        private void ToggleKeyboardSharing()
        {
            IsKeyboardShared = !IsKeyboardShared;
            UpdateGlobalFeatures();
        }

        [RelayCommand]
        private void ToggleMouseSharing()
        {
            IsMouseShared = !IsMouseShared;
            UpdateGlobalFeatures();
        }

        [RelayCommand]
        private void ToggleClipboardSharing()
        {
            IsClipboardShared = !IsClipboardShared;
            UpdateGlobalFeatures();
        }

        [RelayCommand]
        private void ToggleScreenSharing()
        {
            IsScreenShared = !IsScreenShared;
            UpdateGlobalFeatures();
        }

        private void UpdateGlobalFeatures()
        {
            EnabledFeatures = SessionFeatures.None;
            
            if (IsKeyboardShared)
                EnabledFeatures = EnabledFeatures.Enable(SessionFeatures.Keyboard);
            if (IsMouseShared)
                EnabledFeatures = EnabledFeatures.Enable(SessionFeatures.Mouse);
            if (IsClipboardShared)
                EnabledFeatures = EnabledFeatures.Enable(SessionFeatures.Clipboard);
            if (IsScreenShared)
                EnabledFeatures = EnabledFeatures.Enable(SessionFeatures.Screen);
        }

        private async Task ShowNotification(string title, string message)
        {
            // TODO: Implement actual notification system
            // For now, just update status
            StatusMessage = $"{title}: {message}";
            Title = $"{title} - Cross Input Share";
        }

        /// <summary>
        /// Gets whether there are any connected devices
        /// </summary>
        public bool HasConnectedDevices => ConnectedDevices.Count > 0;
    }

    /// <summary>
    /// Connection status enumeration
    /// </summary>
    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }
}