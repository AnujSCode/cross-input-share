using System;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Represents a keyboard input event.
    /// </summary>
    public class KeyboardEvent
    {
        /// <summary>
        /// Unique identifier for this event.
        /// </summary>
        public Guid EventId { get; }
        
        /// <summary>
        /// The device that generated this event.
        /// </summary>
        public Guid SourceDeviceId { get; }
        
        /// <summary>
        /// Virtual key code (platform-independent representation).
        /// </summary>
        public int VirtualKeyCode { get; }
        
        /// <summary>
        /// Whether the key is being pressed down (true) or released (false).
        /// </summary>
        public bool IsKeyDown { get; }
        
        /// <summary>
        /// Whether this is an extended key (e.g., right Alt, Ctrl).
        /// </summary>
        public bool IsExtendedKey { get; }
        
        /// <summary>
        /// Timestamp when the event occurred (UTC).
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Optional Unicode character for the key.
        /// </summary>
        public char? Character { get; }
        
        /// <summary>
        /// Modifier keys state (Shift, Ctrl, Alt, Win).
        /// </summary>
        public ModifierKeys Modifiers { get; }

        public KeyboardEvent(Guid sourceDeviceId, int virtualKeyCode, bool isKeyDown, bool isExtendedKey, ModifierKeys modifiers, char? character = null)
        {
            EventId = Guid.NewGuid();
            SourceDeviceId = sourceDeviceId;
            VirtualKeyCode = virtualKeyCode;
            IsKeyDown = isKeyDown;
            IsExtendedKey = isExtendedKey;
            Modifiers = modifiers;
            Character = character;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a mouse input event.
    /// </summary>
    public class MouseEvent
    {
        /// <summary>
        /// Unique identifier for this event.
        /// </summary>
        public Guid EventId { get; }
        
        /// <summary>
        /// The device that generated this event.
        /// </summary>
        public Guid SourceDeviceId { get; }
        
        /// <summary>
        /// Type of mouse event.
        /// </summary>
        public MouseEventType EventType { get; }
        
        /// <summary>
        /// X coordinate (relative or absolute depending on event type).
        /// </summary>
        public int X { get; }
        
        /// <summary>
        /// Y coordinate (relative or absolute depending on event type).
        /// </summary>
        public int Y { get; }
        
        /// <summary>
        /// Mouse wheel delta (positive for up/away, negative for down/toward).
        /// </summary>
        public int WheelDelta { get; }
        
        /// <summary>
        /// Which mouse button is involved (if applicable).
        /// </summary>
        public MouseButton Button { get; }
        
        /// <summary>
        /// Whether the button is being pressed down (true) or released (false).
        /// </summary>
        public bool IsButtonDown { get; }
        
        /// <summary>
        /// Timestamp when the event occurred (UTC).
        /// </summary>
        public DateTime Timestamp { get; }

        public MouseEvent(Guid sourceDeviceId, MouseEventType eventType, int x, int y, MouseButton button = MouseButton.None, bool isButtonDown = false, int wheelDelta = 0)
        {
            EventId = Guid.NewGuid();
            SourceDeviceId = sourceDeviceId;
            EventType = eventType;
            X = x;
            Y = y;
            Button = button;
            IsButtonDown = isButtonDown;
            WheelDelta = wheelDelta;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents clipboard data to be synchronized.
    /// </summary>
    public class ClipboardData
    {
        /// <summary>
        /// Unique identifier for this clipboard operation.
        /// </summary>
        public Guid OperationId { get; }
        
        /// <summary>
        /// The device that generated this clipboard data.
        /// </summary>
        public Guid SourceDeviceId { get; }
        
        /// <summary>
        /// Format of the clipboard data.
        /// </summary>
        public ClipboardFormat Format { get; }
        
        /// <summary>
        /// The actual clipboard data.
        /// </summary>
        public byte[] Data { get; }
        
        /// <summary>
        /// Optional text representation (for text formats).
        /// </summary>
        public string Text { get; }
        
        /// <summary>
        /// Timestamp when the clipboard data was copied (UTC).
        /// </summary>
        public DateTime Timestamp { get; }

        public ClipboardData(Guid sourceDeviceId, ClipboardFormat format, byte[] data, string text = null)
        {
            OperationId = Guid.NewGuid();
            SourceDeviceId = sourceDeviceId;
            Format = format;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Text = text;
            Timestamp = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Creates a text clipboard data object.
        /// </summary>
        public static ClipboardData CreateText(Guid sourceDeviceId, string text)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(text);
            return new ClipboardData(sourceDeviceId, ClipboardFormat.Text, data, text);
        }
        
        /// <summary>
        /// Creates an image clipboard data object.
        /// </summary>
        public static ClipboardData CreateImage(Guid sourceDeviceId, byte[] imageData)
        {
            return new ClipboardData(sourceDeviceId, ClipboardFormat.Image, imageData);
        }
        
        /// <summary>
        /// Creates a file list clipboard data object.
        /// </summary>
        public static ClipboardData CreateFileList(Guid sourceDeviceId, string[] filePaths)
        {
            var data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(filePaths);
            return new ClipboardData(sourceDeviceId, ClipboardFormat.FileList, data);
        }
    }

    /// <summary>
    /// Represents screen data for sharing.
    /// </summary>
    public class ScreenData
    {
        /// <summary>
        /// Unique identifier for this screen data frame.
        /// </summary>
        public Guid FrameId { get; }
        
        /// <summary>
        /// The device that is sharing its screen.
        /// </summary>
        public Guid SourceDeviceId { get; }
        
        /// <summary>
        /// Format of the screen data.
        /// </summary>
        public ScreenFormat Format { get; }
        
        /// <summary>
        /// The screen data (compressed image, video frame, etc.).
        /// </summary>
        public byte[] Data { get; }
        
        /// <summary>
        /// Width of the screen or region.
        /// </summary>
        public int Width { get; }
        
        /// <summary>
        /// Height of the screen or region.
        /// </summary>
        public int Height { get; }
        
        /// <summary>
        /// Timestamp when the screen was captured (UTC).
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Frame sequence number for video streams.
        /// </summary>
        public long FrameNumber { get; }
        
        /// <summary>
        /// Whether this is a key frame (for video compression).
        /// </summary>
        public bool IsKeyFrame { get; }

        public ScreenData(Guid sourceDeviceId, ScreenFormat format, byte[] data, int width, int height, long frameNumber = 0, bool isKeyFrame = false)
        {
            FrameId = Guid.NewGuid();
            SourceDeviceId = sourceDeviceId;
            Format = format;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Width = width;
            Height = height;
            FrameNumber = frameNumber;
            IsKeyFrame = isKeyFrame;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Modifier keys state.
    /// </summary>
    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Shift = 1 << 0,
        Control = 1 << 1,
        Alt = 1 << 2,
        Windows = 1 << 3,
        CapsLock = 1 << 4,
        NumLock = 1 << 5,
        ScrollLock = 1 << 6
    }

    /// <summary>
    /// Types of mouse events.
    /// </summary>
    public enum MouseEventType
    {
        Move,
        MoveAbsolute,
        ButtonDown,
        ButtonUp,
        ButtonClick,
        ButtonDoubleClick,
        Wheel,
        HorizontalWheel
    }

    /// <summary>
    /// Mouse buttons.
    /// </summary>
    public enum MouseButton
    {
        None,
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    }

    /// <summary>
    /// Clipboard data formats.
    /// </summary>
    public enum ClipboardFormat
    {
        Text,
        RichText,
        Html,
        Image,
        FileList,
        Custom
    }

    /// <summary>
    /// Screen data formats.
    /// </summary>
    public enum ScreenFormat
    {
        RawBitmap,
        Jpeg,
        Png,
        H264,
        Vp8,
        Vp9
    }
}