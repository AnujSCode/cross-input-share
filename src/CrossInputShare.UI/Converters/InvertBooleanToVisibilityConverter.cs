using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace CrossInputShare.UI.Converters
{
    /// <summary>
    /// Converts boolean values to Visibility enum values (inverted)
    /// </summary>
    public class InvertBooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to Visibility (inverted)
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        /// <summary>
        /// Converts Visibility back to boolean (not implemented)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}