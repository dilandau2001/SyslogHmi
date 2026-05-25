using System;
using System.Globalization;
using System.Windows.Data;

namespace SyslogHmi.Converters
{
    public class SingleLineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {   
                return text.Replace("\r\n", " ↵ ").Replace("\n", " ↵ ");
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}