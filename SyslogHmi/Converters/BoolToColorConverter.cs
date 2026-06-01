using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyslogHmi.Converters;

/// <summary>
/// Converts boolean to a brush color (True = green, False = gray).
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? new SolidColorBrush(Color.FromRgb(0, 128, 0)) // Green
                : new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}