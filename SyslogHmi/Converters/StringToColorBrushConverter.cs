using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// Converts a string containing a hex color or named color into a SolidColorBrush for UI binding.
    /// </summary>
    public class StringToColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                try
                {
                    // Try to parse as hex color (e.g., "#FFCCCC")
                    if (colorString.StartsWith("#"))
                    {
                        var color = ColorConverter.ConvertFromString(colorString);
                        if (color is Color parsedColor)
                        {
                            return new SolidColorBrush(parsedColor);
                        }
                    }
                    // Try to parse as named color (e.g., "Red", "Transparent")
                    else
                    {
                        var color = ColorConverter.ConvertFromString(colorString);
                        if (color is Color parsedColor)
                        {
                            return new SolidColorBrush(parsedColor);
                        }
                    }
                }
                catch
                {
                    // Fallback to transparent if parsing fails
                }
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
