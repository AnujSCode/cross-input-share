using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossInputShare.Core.Models;

namespace CrossInputShare.Platform.Interfaces
{
    /// <summary>
    /// Platform-specific clipboard service for monitoring and manipulating clipboard content.
    /// </summary>
    public interface IPlatformClipboardService : IDisposable
    {
        /// <summary>
        /// Event raised when clipboard content changes.
        /// </summary>
        event EventHandler<ClipboardChangedEventArgs> ClipboardChanged;

        /// <summary>
        /// Gets whether clipboard monitoring is active.
        /// </summary>
        bool IsMonitoringActive { get; }

        /// <summary>
        /// Starts monitoring clipboard changes.
        /// </summary>
        /// <param name="monitorOptions">Options for clipboard monitoring</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if monitoring started successfully</returns>
        Task<bool> StartMonitoringAsync(ClipboardMonitorOptions monitorOptions, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops monitoring clipboard changes.
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Gets the current clipboard content in the specified format.
        /// </summary>
        /// <param name="format">The format to retrieve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Clipboard data or null if not available in the requested format</returns>
        Task<ClipboardData?> GetClipboardContentAsync(ClipboardFormat format, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all available clipboard formats.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of available formats</returns>
        Task<IReadOnlyList<ClipboardFormat>> GetAvailableFormatsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Sets clipboard content.
        /// </summary>
        /// <param name="clipboardData">The clipboard data to set</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the operation succeeded</returns>
        Task<bool> SetClipboardContentAsync(ClipboardData clipboardData, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clears the clipboard.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ClearClipboardAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets clipboard history (if supported by platform).
        /// </summary>
        /// <param name="maxItems">Maximum number of history items to retrieve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Clipboard history items</returns>
        Task<IReadOnlyList<ClipboardHistoryItem>> GetClipboardHistoryAsync(int maxItems = 50, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Pins an item to clipboard history (if supported).
        /// </summary>
        /// <param name="itemId">The item to pin</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the operation succeeded</returns>
        Task<bool> PinClipboardItemAsync(string itemId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clears clipboard history (if supported).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ClearClipboardHistoryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a specific clipboard format is supported on this platform.
        /// </summary>
        /// <param name="format">The format to check</param>
        /// <returns>True if the format is supported</returns>
        bool IsFormatSupported(ClipboardFormat format);
    }

    /// <summary>
    /// Options for clipboard monitoring.
    /// </summary>
    public class ClipboardMonitorOptions
    {
        /// <summary>
        /// Whether to monitor text clipboard changes.
        /// </summary>
        public bool MonitorText { get; set; } = true;
        
        /// <summary>
        /// Whether to monitor image clipboard changes.
        /// </summary>
        public bool MonitorImages { get; set; } = true;
        
        /// <summary>
        /// Whether to monitor file list clipboard changes.
        /// </summary>
        public bool MonitorFileLists { get; set; } = true;
        
        /// <summary>
        /// Whether to monitor rich text clipboard changes.
        /// </summary>
        public bool MonitorRichText { get; set; } = false;
        
        /// <summary>
        /// Whether to monitor HTML clipboard changes.
        /// </summary>
        public bool MonitorHtml { get; set; } = false;
        
        /// <summary>
        /// Whether to monitor custom format clipboard changes.
        /// </summary>
        public bool MonitorCustomFormats { get; set; } = false;
        
        /// <summary>
        /// Whether to ignore clipboard changes made by this application.
        /// </summary>
        public bool IgnoreOwnChanges { get; set; } = true;
        
        /// <summary>
        /// Minimum time between clipboard change notifications (in milliseconds).
        /// </summary>
        public int DebounceIntervalMs { get; set; } = 100;
        
        /// <summary>
        /// Whether to capture clipboard content when monitoring starts.
        /// </summary>
        public bool CaptureInitialContent { get; set; } = true;
    }

    /// <summary>
    /// Event arguments for clipboard changes.
    /// </summary>
    public class ClipboardChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new clipboard data.
        /// </summary>
        public ClipboardData ClipboardData { get; }
        
        /// <summary>
        /// Whether the change was made by this application.
        /// </summary>
        public bool IsOwnChange { get; }
        
        /// <summary>
        /// Timestamp when the change occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        public ClipboardChangedEventArgs(ClipboardData clipboardData, bool isOwnChange)
        {
            ClipboardData = clipboardData ?? throw new ArgumentNullException(nameof(clipboardData));
            IsOwnChange = isOwnChange;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents an item in clipboard history.
    /// </summary>
    public class ClipboardHistoryItem
    {
        /// <summary>
        /// Unique identifier for this history item.
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// The clipboard data.
        /// </summary>
        public ClipboardData Data { get; }
        
        /// <summary>
        /// When this item was added to clipboard history.
        /// </summary>
        public DateTime AddedAt { get; }
        
        /// <summary>
        /// Whether this item is pinned (won't be automatically removed).
        /// </summary>
        public bool IsPinned { get; }
        
        /// <summary>
        /// Number of times this item has been accessed.
        /// </summary>
        public int AccessCount { get; }

        public ClipboardHistoryItem(string id, ClipboardData data, DateTime addedAt, bool isPinned = false, int accessCount = 0)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            AddedAt = addedAt;
            IsPinned = isPinned;
            AccessCount = accessCount;
        }
    }
}