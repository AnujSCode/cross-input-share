using System;

namespace CrossInputShare.Core.Models
{
    /// <summary>
    /// Represents the features that can be enabled or disabled in a session.
    /// Users can toggle these features based on their needs and security preferences.
    /// </summary>
    [Flags]
    public enum SessionFeatures
    {
        /// <summary>
        /// No features enabled (default for security).
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Keyboard input sharing.
        /// When enabled, keyboard events are captured and sent to connected devices.
        /// </summary>
        Keyboard = 1 << 0,
        
        /// <summary>
        /// Mouse input sharing.
        /// When enabled, mouse events (movement, clicks, scroll) are captured and sent.
        /// </summary>
        Mouse = 1 << 1,
        
        /// <summary>
        /// Clipboard synchronization.
        /// When enabled, clipboard content is synchronized between devices.
        /// </summary>
        Clipboard = 1 << 2,
        
        /// <summary>
        /// Screen sharing (optional).
        /// When enabled, screen content can be shared (view-only or remote control).
        /// </summary>
        Screen = 1 << 3,
        
        /// <summary>
        /// File transfer capability.
        /// When enabled, files can be transferred between devices.
        /// </summary>
        FileTransfer = 1 << 4,
        
        /// <summary>
        /// Audio sharing capability.
        /// When enabled, audio can be streamed between devices.
        /// </summary>
        Audio = 1 << 5
    }

    /// <summary>
    /// Extension methods for SessionFeatures enum.
    /// Provides convenient methods for working with feature flags.
    /// </summary>
    public static class SessionFeaturesExtensions
    {
        /// <summary>
        /// Default features for a new session.
        /// Includes keyboard, mouse, and clipboard by default for basic input sharing.
        /// </summary>
        public static SessionFeatures Default => SessionFeatures.Keyboard | SessionFeatures.Mouse | SessionFeatures.Clipboard;

        /// <summary>
        /// All available features enabled.
        /// Useful for testing or power users who want everything.
        /// </summary>
        public static SessionFeatures All => SessionFeatures.Keyboard | SessionFeatures.Mouse | SessionFeatures.Clipboard |
                                           SessionFeatures.Screen | SessionFeatures.FileTransfer | SessionFeatures.Audio;

        /// <summary>
        /// Basic input features (keyboard and mouse only).
        /// Minimal feature set for simple remote control.
        /// </summary>
        public static SessionFeatures BasicInput => SessionFeatures.Keyboard | SessionFeatures.Mouse;

        /// <summary>
        /// Checks if a specific feature is enabled.
        /// </summary>
        /// <param name="features">The feature set to check</param>
        /// <param name="feature">The feature to check for</param>
        /// <returns>True if the feature is enabled</returns>
        public static bool HasFeature(this SessionFeatures features, SessionFeatures feature)
        {
            return (features & feature) == feature;
        }

        /// <summary>
        /// Enables a specific feature.
        /// </summary>
        /// <param name="features">The feature set to modify</param>
        /// <param name="feature">The feature to enable</param>
        /// <returns>The modified feature set</returns>
        public static SessionFeatures Enable(this SessionFeatures features, SessionFeatures feature)
        {
            return features | feature;
        }

        /// <summary>
        /// Disables a specific feature.
        /// </summary>
        /// <param name="features">The feature set to modify</param>
        /// <param name="feature">The feature to disable</param>
        /// <returns>The modified feature set</returns>
        public static SessionFeatures Disable(this SessionFeatures features, SessionFeatures feature)
        {
            return features & ~feature;
        }

        /// <summary>
        /// Toggles a specific feature.
        /// </summary>
        /// <param name="features">The feature set to modify</param>
        /// <param name="feature">The feature to toggle</param>
        /// <returns>The modified feature set</returns>
        public static SessionFeatures Toggle(this SessionFeatures features, SessionFeatures feature)
        {
            return features.HasFeature(feature) ? features.Disable(feature) : features.Enable(feature);
        }

        /// <summary>
        /// Gets a user-friendly description of the enabled features.
        /// </summary>
        /// <param name="features">The feature set to describe</param>
        /// <returns>A comma-separated list of enabled features</returns>
        public static string GetDescription(this SessionFeatures features)
        {
            if (features == SessionFeatures.None)
                return "None";
            
            var descriptions = new System.Collections.Generic.List<string>();
            
            if (features.HasFeature(SessionFeatures.Keyboard))
                descriptions.Add("Keyboard");
            if (features.HasFeature(SessionFeatures.Mouse))
                descriptions.Add("Mouse");
            if (features.HasFeature(SessionFeatures.Clipboard))
                descriptions.Add("Clipboard");
            if (features.HasFeature(SessionFeatures.Screen))
                descriptions.Add("Screen");
            if (features.HasFeature(SessionFeatures.FileTransfer))
                descriptions.Add("File Transfer");
            if (features.HasFeature(SessionFeatures.Audio))
                descriptions.Add("Audio");
            
            return string.Join(", ", descriptions);
        }

        /// <summary>
        /// Validates that the feature set is reasonable and secure.
        /// For example, screen sharing without verification might be restricted.
        /// </summary>
        /// <param name="features">The feature set to validate</param>
        /// <param name="isSessionVerified">Whether the session has been manually verified</param>
        /// <returns>True if the feature set is valid for the session state</returns>
        public static bool IsValidForSession(this SessionFeatures features, bool isSessionVerified)
        {
            // If session is not verified, restrict sensitive features
            if (!isSessionVerified)
            {
                // Screen sharing requires verification due to security implications
                if (features.HasFeature(SessionFeatures.Screen))
                    return false;
                
                // File transfer might also require verification
                if (features.HasFeature(SessionFeatures.FileTransfer))
                    return false;
            }
            
            return true;
        }
    }
}