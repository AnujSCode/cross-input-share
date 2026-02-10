using Microsoft.UI.Xaml.Data;
using System;
using Windows.UI;

namespace CrossInputShare.UI.Converters
{
    /// <summary>
    /// Converts ConnectionStatus to Color for UI display
    /// </summary>
    public class ConnectionStatusToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts ConnectionStatus to Color
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ViewModels.ConnectionStatus status)
            {
                return status switch
                {
                    ViewModels.ConnectionStatus.Connected => Color.FromArgb(255, 76, 175, 80),   // Green
                    ViewModels.ConnectionStatus.Connecting => Color.FromArgb(255, 255, 193, 7),  // Yellow/Amber
                    ViewModels.ConnectionStatus.Disconnected => Color.FromArgb(255, 244, 67, 54), // Red
                    ViewModels.ConnectionStatus.Error => Color.FromArgb(255, 156, 39, 176),      // Purple
                    _ => Color.FromArgb(255, 117, 117, 117)                                     // Gray
                };
            }

            return Color.FromArgb(255, 117, 117, 117); // Default gray
        }

        /// <summary>
        /// Converts Color back to ConnectionStatus (not implemented)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}