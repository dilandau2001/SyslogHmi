using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// Converts a null value to Visibility.Collapsed and non-null to Visibility.Visible.
    /// If parameter is the string "Inverted" the logic is reversed.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var inverted = (parameter as string) == "Inverted";
            var isNull = value == null;
            if (inverted)
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
