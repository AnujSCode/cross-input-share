using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace CrossInputShare.UI.Converters
{
    /// <summary>
    /// Converts boolean values to Visibility enum values
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Gets or sets a value indicating whether to invert the conversion
        /// </summary>
        public bool Invert { get; set; }

        /// <summary>
        /// Converts a boolean value to Visibility
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                boolValue = Invert ? !boolValue : boolValue;
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
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