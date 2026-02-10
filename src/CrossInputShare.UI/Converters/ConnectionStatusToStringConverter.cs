using Microsoft.UI.Xaml.Data;
using System;

namespace CrossInputShare.UI.Converters
{
    /// <summary>
    /// Converts ConnectionStatus to string for display
    /// </summary>
    public class ConnectionStatusToStringConverter : IValueConverter
    {
        /// <summary>
        /// Converts ConnectionStatus to display string
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ViewModels.ConnectionStatus status)
            {
                return status switch
                {
                    ViewModels.ConnectionStatus.Connected => "Connected",
                    ViewModels.ConnectionStatus.Connecting => "Connecting...",
                    ViewModels.ConnectionStatus.Disconnected => "Disconnected",
                    ViewModels.ConnectionStatus.Error => "Connection Error",
                    _ => "Unknown"
                };
            }

            return "Unknown";
        }

        /// <summary>
        /// Converts string back to ConnectionStatus (not implemented)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}