using CrossInputShare.Core.Models;
using Microsoft.UI.Xaml.Data;
using System;

namespace CrossInputShare.UI.Converters
{
    /// <summary>
    /// Converts SessionFeatures to string for display
    /// </summary>
    public class SessionFeaturesToStringConverter : IValueConverter
    {
        /// <summary>
        /// Converts SessionFeatures to display string
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SessionFeatures features)
            {
                return features.GetDescription();
            }

            return "None";
        }

        /// <summary>
        /// Converts string back to SessionFeatures (not implemented)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}