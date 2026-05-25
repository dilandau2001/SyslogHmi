using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SyslogHmi.Helpers;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// Converts a friendly color name (e.g., "Red", "Blue", "Green") to a SolidColorBrush for UI binding.
    /// Uses ColorHelper to map friendly names to their hex color equivalents.
    /// Returns white brush as a default fallback if the color name is not recognized.
    /// </summary>
    public class FriendlyColorNameToBrushConverter : IValueConverter
    {
        /// <summary>
        /// Converts a friendly color name string to a SolidColorBrush.
        /// </summary>
        /// <param name="value">The friendly color name (string) to convert.</param>
        /// <param name="targetType">The target type for the conversion (not used).</param>
        /// <param name="parameter">An optional parameter (not used).</param>
        /// <param name="culture">The culture information for the conversion (not used).</param>
        /// <returns>A SolidColorBrush representing the friendly color name, or white if conversion fails.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Check if the input value is a string (friendly color name)
            if (value is string colorName)
            {
                // Get the hex color equivalent from the ColorHelper utility
                var hexColor = ColorHelper.GetHexFromFriendlyName(colorName);
                try
                {
                    // Convert hex string to Color and create a SolidColorBrush
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
                }
                catch
                {
                    // If conversion fails, return white brush as fallback
                    return new SolidColorBrush(Colors.White);
                }
            }
            // Default: return white brush if value is not a string
            return new SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Converts a SolidColorBrush back to a friendly color name string.
        /// This operation is not supported by this converter.
        /// </summary>
        /// <param name="value">The SolidColorBrush to convert back.</param>
        /// <param name="targetType">The target type for the conversion (not used).</param>
        /// <param name="parameter">An optional parameter (not used).</param>
        /// <param name="culture">The culture information for the conversion (not used).</param>
        /// <returns>Not implemented; throws NotImplementedException.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
