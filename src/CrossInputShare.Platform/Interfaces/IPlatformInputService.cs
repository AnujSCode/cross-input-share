using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Platform.Interfaces
{
    /// <summary>
    /// Platform-specific input service for capturing and injecting input events.
    /// </summary>
    public interface IPlatformInputService : IDisposable
    {
        /// <summary>
        /// Event raised when a keyboard event is captured.
        /// </summary>
        event EventHandler<KeyboardEvent> KeyboardEventCaptured;
        
        /// <summary>
        /// Event raised when a mouse event is captured.
        /// </summary>
        event EventHandler<MouseEvent> MouseEventCaptured;
        
        /// <summary>
        /// Event raised when an input device is connected or disconnected.
        /// </summary>
        event EventHandler<InputDeviceEventArgs> InputDeviceChanged;

        /// <summary>
        /// Gets whether input capture is currently active.
        /// </summary>
        bool IsCaptureActive { get; }
        
        /// <summary>
        /// Gets whether input injection is currently active.
        /// </summary>
        bool IsInjectionActive { get; }

        /// <summary>
        /// Starts capturing input events from the system.
        /// </summary>
        /// <param name="captureOptions">Options for what to capture</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if capture started successfully</returns>
        Task<bool> StartCaptureAsync(InputCaptureOptions captureOptions, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops capturing input events.
        /// </summary>
        Task StopCaptureAsync();
        
        /// <summary>
        /// Starts injecting input events into the system.
        /// </summary>
        /// <param name="injectionOptions">Options for input injection</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if injection started successfully</returns>
        Task<bool> StartInjectionAsync(InputInjectionOptions injectionOptions, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops injecting input events.
        /// </summary>
        Task StopInjectionAsync();

        /// <summary>
        /// Injects a keyboard event into the system.
        /// </summary>
        /// <param name="keyboardEvent">The keyboard event to inject</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InjectKeyboardEventAsync(KeyboardEvent keyboardEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Injects a mouse event into the system.
        /// </summary>
        /// <param name="mouseEvent">The mouse event to inject</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InjectMouseEventAsync(MouseEvent mouseEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Injects multiple input events atomically.
        /// </summary>
        /// <param name="keyboardEvents">Keyboard events to inject</param>
        /// <param name="mouseEvents">Mouse events to inject</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InjectInputEventsAsync(IEnumerable<KeyboardEvent> keyboardEvents, IEnumerable<MouseEvent> mouseEvents, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets information about available input devices.
        /// </summary>
        /// <returns>List of input device information</returns>
        IReadOnlyList<InputDeviceInfo> GetInputDevices();
        
        /// <summary>
        /// Gets the current cursor position.
        /// </summary>
        /// <returns>Current cursor position</returns>
        (int x, int y) GetCursorPosition();
        
        /// <summary>
        /// Sets the cursor position.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        void SetCursorPosition(int x, int y);
    }

    /// <summary>
    /// Options for input capture.
    /// </summary>
    public class InputCaptureOptions
    {
        /// <summary>
        /// Whether to capture keyboard events.
        /// </summary>
        public bool CaptureKeyboard { get; set; } = true;
        
        /// <summary>
        /// Whether to capture mouse events.
        /// </summary>
        public bool CaptureMouse { get; set; } = true;
        
        /// <summary>
        /// Whether to capture relative mouse movements (vs absolute).
        /// </summary>
        public bool CaptureRelativeMouse { get; set; } = true;
        
        /// <summary>
        /// Whether to capture mouse wheel events.
        /// </summary>
        public bool CaptureMouseWheel { get; set; } = true;
        
        /// <summary>
        /// Whether to block captured events from reaching the system.
        /// </summary>
        public bool BlockCapturedEvents { get; set; } = false;
        
        /// <summary>
        /// Specific devices to capture (empty for all devices).
        /// </summary>
        public IReadOnlyList<string> DeviceFilter { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Whether to capture low-level events (requires elevated privileges).
        /// </summary>
        public bool UseLowLevelCapture { get; set; } = false;
    }

    /// <summary>
    /// Options for input injection.
    /// </summary>
    public class InputInjectionOptions
    {
        /// <summary>
        /// Whether to inject keyboard events.
        /// </summary>
        public bool InjectKeyboard { get; set; } = true;
        
        /// <summary>
        /// Whether to inject mouse events.
        /// </summary>
        public bool InjectMouse { get; set; } = true;
        
        /// <summary>
        /// Whether to use relative mouse coordinates.
        /// </summary>
        public bool UseRelativeMouse { get; set; } = true;
        
        /// <summary>
        /// Whether to simulate natural typing delays.
        /// </summary>
        public bool SimulateTypingDelays { get; set; } = false;
        
        /// <summary>
        /// Delay between keystrokes in milliseconds (if simulating delays).
        /// </summary>
        public int KeystrokeDelayMs { get; set; } = 10;
        
        /// <summary>
        /// Whether to require elevated privileges for injection.
        /// </summary>
        public bool RequireElevation { get; set; } = false;
    }

    /// <summary>
    /// Event arguments for input device changes.
    /// </summary>
    public class InputDeviceEventArgs : EventArgs
    {
        /// <summary>
        /// The device that changed.
        /// </summary>
        public InputDeviceInfo Device { get; }
        
        /// <summary>
        /// Whether the device was connected (true) or disconnected (false).
        /// </summary>
        public bool IsConnected { get; }

        public InputDeviceEventArgs(InputDeviceInfo device, bool isConnected)
        {
            Device = device;
            IsConnected = isConnected;
        }
    }

    /// <summary>
    /// Information about an input device.
    /// </summary>
    public class InputDeviceInfo
    {
        /// <summary>
        /// Unique identifier for the device.
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Device name.
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Device type.
        /// </summary>
        public InputDeviceType Type { get; }
        
        /// <summary>
        /// Vendor ID (if available).
        /// </summary>
        public int? VendorId { get; }
        
        /// <summary>
        /// Product ID (if available).
        /// </summary>
        public int? ProductId { get; }
        
        /// <summary>
        /// Whether the device is currently connected.
        /// </summary>
        public bool IsConnected { get; }
        
        /// <summary>
        /// Additional device-specific properties.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties { get; }

        public InputDeviceInfo(string id, string name, InputDeviceType type, bool isConnected, int? vendorId = null, int? productId = null, IReadOnlyDictionary<string, object>? properties = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            IsConnected = isConnected;
            VendorId = vendorId;
            ProductId = productId;
            Properties = properties ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Types of input devices.
    /// </summary>
    public enum InputDeviceType
    {
        Keyboard,
        Mouse,
        Touchpad,
        Touchscreen,
        Gamepad,
        Joystick,
        Tablet,
        Other
    }
}