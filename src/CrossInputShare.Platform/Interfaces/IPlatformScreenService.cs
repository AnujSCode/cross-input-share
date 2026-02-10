using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Platform.Interfaces
{
    /// <summary>
    /// Platform-specific screen service for capturing and streaming screen content.
    /// </summary>
    public interface IPlatformScreenService : IDisposable
    {
        /// <summary>
        /// Event raised when a screen frame is captured.
        /// </summary>
        event EventHandler<ScreenFrameCapturedEventArgs> ScreenFrameCaptured;
        
        /// <summary>
        /// Event raised when screen configuration changes (display added/removed, resolution changed).
        /// </summary>
        event EventHandler<ScreenConfigurationChangedEventArgs> ScreenConfigurationChanged;

        /// <summary>
        /// Gets whether screen capture is currently active.
        /// </summary>
        bool IsCaptureActive { get; }
        
        /// <summary>
        /// Gets the current capture configuration.
        /// </summary>
        ScreenCaptureConfig CurrentConfig { get; }

        /// <summary>
        /// Gets information about available displays/screens.
        /// </summary>
        /// <returns>List of display information</returns>
        IReadOnlyList<DisplayInfo> GetDisplays();
        
        /// <summary>
        /// Gets the primary display.
        /// </summary>
        /// <returns>The primary display information</returns>
        DisplayInfo GetPrimaryDisplay();

        /// <summary>
        /// Starts capturing screen content.
        /// </summary>
        /// <param name="config">Capture configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if capture started successfully</returns>
        Task<bool> StartCaptureAsync(ScreenCaptureConfig config, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops capturing screen content.
        /// </summary>
        Task StopCaptureAsync();
        
        /// <summary>
        /// Pauses screen capture (temporarily stops capturing frames).
        /// </summary>
        Task PauseCaptureAsync();
        
        /// <summary>
        /// Resumes screen capture after pausing.
        /// </summary>
        Task ResumeCaptureAsync();

        /// <summary>
        /// Captures a single screen frame.
        /// </summary>
        /// <param name="config">Capture configuration for this single frame</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The captured screen data or null if capture failed</returns>
        Task<ScreenData?> CaptureSingleFrameAsync(ScreenCaptureConfig config, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Captures a specific region of the screen.
        /// </summary>
        /// <param name="region">The region to capture</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The captured screen data or null if capture failed</returns>
        Task<ScreenData?> CaptureRegionAsync(ScreenRegion region, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Captures a specific window.
        /// </summary>
        /// <param name="windowHandle">Platform-specific window handle/identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The captured screen data or null if capture failed</returns>
        Task<ScreenData?> CaptureWindowAsync(object windowHandle, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current cursor position and image.
        /// </summary>
        /// <returns>Cursor information</returns>
        CursorInfo GetCursorInfo();
        
        /// <summary>
        /// Gets information about visible windows.
        /// </summary>
        /// <returns>List of window information</returns>
        IReadOnlyList<WindowInfo> GetWindows();
    }

    /// <summary>
    /// Configuration for screen capture.
    /// </summary>
    public class ScreenCaptureConfig
    {
        /// <summary>
        /// Which display to capture (null for all displays).
        /// </summary>
        public string? DisplayId { get; set; }
        
        /// <summary>
        /// Specific region to capture (null for entire display).
        /// </summary>
        public ScreenRegion? Region { get; set; }
        
        /// <summary>
        /// Target frame rate (frames per second).
        /// </summary>
        public int FrameRate { get; set; } = 30;
        
        /// <summary>
        /// Output format for screen data.
        /// </summary>
        public ScreenFormat OutputFormat { get; set; } = ScreenFormat.Jpeg;
        
        /// <summary>
        /// Quality setting for compressed formats (0-100).
        /// </summary>
        public int Quality { get; set; } = 85;
        
        /// <summary>
        /// Whether to capture the cursor.
        /// </summary>
        public bool CaptureCursor { get; set; } = true;
        
        /// <summary>
        /// Whether to use hardware acceleration if available.
        /// </summary>
        public bool UseHardwareAcceleration { get; set; } = true;
        
        /// <summary>
        /// Whether to capture only when there are changes (incremental capture).
        /// </summary>
        public bool UseIncrementalCapture { get; set; } = true;
        
        /// <summary>
        /// Maximum resolution (width). 0 for no limit.
        /// </summary>
        public int MaxWidth { get; set; } = 1920;
        
        /// <summary>
        /// Maximum resolution (height). 0 for no limit.
        /// </summary>
        public int MaxHeight { get; set; } = 1080;
        
        /// <summary>
        /// Whether to downscale if necessary to meet max resolution.
        /// </summary>
        public bool AllowDownscaling { get; set; } = true;
        
        /// <summary>
        /// Whether to maintain aspect ratio when downscaling.
        /// </summary>
        public bool MaintainAspectRatio { get; set; } = true;
        
        /// <summary>
        /// Color format for raw capture.
        /// </summary>
        public ColorFormat ColorFormat { get; set; } = ColorFormat.Bgra32;
    }

    /// <summary>
    /// A region of the screen to capture.
    /// </summary>
    public class ScreenRegion
    {
        /// <summary>
        /// X coordinate of the region's left edge.
        /// </summary>
        public int X { get; }
        
        /// <summary>
        /// Y coordinate of the region's top edge.
        /// </summary>
        public int Y { get; }
        
        /// <summary>
        /// Width of the region.
        /// </summary>
        public int Width { get; }
        
        /// <summary>
        /// Height of the region.
        /// </summary>
        public int Height { get; }
        
        /// <summary>
        /// Which display this region is on.
        /// </summary>
        public string DisplayId { get; }

        public ScreenRegion(int x, int y, int width, int height, string displayId)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            DisplayId = displayId ?? throw new ArgumentNullException(nameof(displayId));
        }
        
        /// <summary>
        /// Creates a region for an entire display.
        /// </summary>
        public static ScreenRegion ForDisplay(DisplayInfo display)
        {
            return new ScreenRegion(0, 0, display.Width, display.Height, display.Id);
        }
    }

    /// <summary>
    /// Information about a display/screen.
    /// </summary>
    public class DisplayInfo
    {
        /// <summary>
        /// Unique identifier for the display.
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Whether this is the primary display.
        /// </summary>
        public bool IsPrimary { get; }
        
        /// <summary>
        /// Width in pixels.
        /// </summary>
        public int Width { get; }
        
        /// <summary>
        /// Height in pixels.
        /// </summary>
        public int Height { get; }
        
        /// <summary>
        /// Bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; }
        
        /// <summary>
        /// Refresh rate in Hz.
        /// </summary>
        public int RefreshRate { get; }
        
        /// <summary>
        /// DPI scaling factor.
        /// </summary>
        public float DpiScale { get; }
        
        /// <summary>
        /// Physical width in millimeters (if available).
        /// </summary>
        public int? PhysicalWidthMm { get; }
        
        /// <summary>
        /// Physical height in millimeters (if available).
        /// </summary>
        public int? PhysicalHeightMm { get; }
        
        /// <summary>
        /// Display rotation.
        /// </summary>
        public DisplayRotation Rotation { get; }

        public DisplayInfo(string id, string name, bool isPrimary, int width, int height, int bitsPerPixel, int refreshRate, float dpiScale, DisplayRotation rotation, int? physicalWidthMm = null, int? physicalHeightMm = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsPrimary = isPrimary;
            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;
            RefreshRate = refreshRate;
            DpiScale = dpiScale;
            Rotation = rotation;
            PhysicalWidthMm = physicalWidthMm;
            PhysicalHeightMm = physicalHeightMm;
        }
    }

    /// <summary>
    /// Information about a cursor.
    /// </summary>
    public class CursorInfo
    {
        /// <summary>
        /// Current cursor position (screen coordinates).
        /// </summary>
        public (int x, int y) Position { get; }
        
        /// <summary>
        /// Cursor image data (if available).
        /// </summary>
        public byte[]? ImageData { get; }
        
        /// <summary>
        /// Cursor image width.
        /// </summary>
        public int ImageWidth { get; }
        
        /// <summary>
        /// Cursor image height.
        /// </summary>
        public int ImageHeight { get; }
        
        /// <summary>
        /// Cursor hotspot X coordinate (relative to cursor image).
        /// </summary>
        public int HotspotX { get; }
        
        /// <summary>
        /// Cursor hotspot Y coordinate (relative to cursor image).
        /// </summary>
        public int HotspotY { get; }
        
        /// <summary>
        /// Cursor type/ID.
        /// </summary>
        public string CursorType { get; }

        public CursorInfo((int x, int y) position, byte[]? imageData, int imageWidth, int imageHeight, int hotspotX, int hotspotY, string cursorType)
        {
            Position = position;
            ImageData = imageData;
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            HotspotX = hotspotX;
            HotspotY = hotspotY;
            CursorType = cursorType ?? throw new ArgumentNullException(nameof(cursorType));
        }
    }

    /// <summary>
    /// Information about a window.
    /// </summary>
    public class WindowInfo
    {
        /// <summary>
        /// Platform-specific window handle/identifier.
        /// </summary>
        public object Handle { get; }
        
        /// <summary>
        /// Window title.
        /// </summary>
        public string Title { get; }
        
        /// <summary>
        /// Application/process name.
        /// </summary>
        public string ProcessName { get; }
        
        /// <summary>
        /// Process ID.
        /// </summary>
        public int ProcessId { get; }
        
        /// <summary>
        /// Whether the window is visible.
        /// </summary>
        public bool IsVisible { get; }
        
        /// <summary>
        /// Whether the window is minimized.
        /// </summary>
        public bool IsMinimized { get; }
        
        /// <summary>
        /// Whether the window is maximized.
        /// </summary>
        public bool IsMaximized { get; }
        
        /// <summary>
        /// Window bounds (position and size).
        /// </summary>
        public ScreenRegion Bounds { get; }
        
        /// <summary>
        /// Window client area bounds (excluding borders/title bar).
        /// </summary>
        public ScreenRegion ClientBounds { get; }

        public WindowInfo(object handle, string title, string processName, int processId, bool isVisible, bool isMinimized, bool isMaximized, ScreenRegion bounds, ScreenRegion clientBounds)
        {
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            ProcessName = processName ?? throw new ArgumentNullException(nameof(processName));
            ProcessId = processId;
            IsVisible = isVisible;
            IsMinimized = isMinimized;
            IsMaximized = isMaximized;
            Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
            ClientBounds = clientBounds ?? throw new ArgumentNullException(nameof(clientBounds));
        }
    }

    /// <summary>
    /// Event arguments for screen frame capture.
    /// </summary>
    public class ScreenFrameCapturedEventArgs : EventArgs
    {
        /// <summary>
        /// The captured screen data.
        /// </summary>
        public ScreenData ScreenData { get; }
        
        /// <summary>
        /// Capture configuration used.
        /// </summary>
        public ScreenCaptureConfig Config { get; }
        
        /// <summary>
        /// Time taken to capture the frame in milliseconds.
        /// </summary>
        public double CaptureTimeMs { get; }

        public ScreenFrameCapturedEventArgs(ScreenData screenData, ScreenCaptureConfig config, double captureTimeMs)
        {
            ScreenData = screenData ?? throw new ArgumentNullException(nameof(screenData));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            CaptureTimeMs = captureTimeMs;
        }
    }

    /// <summary>
    /// Event arguments for screen configuration changes.
    /// </summary>
    public class ScreenConfigurationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Type of configuration change.
        /// </summary>
        public ScreenConfigurationChangeType ChangeType { get; }
        
        /// <summary>
        /// Affected display (if applicable).
        /// </summary>
        public DisplayInfo? Display { get; }
        
        /// <summary>
        /// Previous configuration (if applicable).
        /// </summary>
        public object? PreviousState { get; }

        public ScreenConfigurationChangedEventArgs(ScreenConfigurationChangeType changeType, DisplayInfo? display = null, object? previousState = null)
        {
            ChangeType = changeType;
            Display = display;
            PreviousState = previousState;
        }
    }

    /// <summary>
    /// Types of screen configuration changes.
    /// </summary>
    public enum ScreenConfigurationChangeType
    {
        DisplayAdded,
        DisplayRemoved,
        DisplayResolutionChanged,
        DisplayRotationChanged,
        DisplayPrimaryChanged,
        DisplayReconfigured
    }

    /// <summary>
    /// Display rotation/orientation.
    /// </summary>
    public enum DisplayRotation
    {
        Identity,
        Rotate90,
        Rotate180,
        Rotate270
    }

    /// <summary>
    /// Color formats for screen capture.
    /// </summary>
    public enum ColorFormat
    {
        Rgb24,
        Bgr24,
        Rgba32,
        Bgra32,
        Argb32,
        Abgr32
    }
}