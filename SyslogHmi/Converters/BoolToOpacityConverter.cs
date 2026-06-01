using System;
using System.Globalization;
using System.Windows.Data;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// Converts a boolean to opacity (1.0 for true, 0.5 for false).
    /// </summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? 1.0 : 0.5;
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
