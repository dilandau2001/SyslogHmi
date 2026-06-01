using System;
using System.Globalization;
using System.Windows.Data;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// Inverts a boolean value (true becomes false, false becomes true).
    /// </summary>
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return true;
        }
    }
}
