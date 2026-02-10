using Microsoft.UI.Xaml.Data;
using System;

namespace CrossInputShare.UI.Converters
{
    /// <summary>
    /// Inverts a boolean value
    /// </summary>
    public class InvertBooleanConverter : IValueConverter
    {
        /// <summary>
        /// Inverts a boolean value
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return false;
        }

        /// <summary>
        /// Inverts a boolean value back
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return false;
        }
    }
}