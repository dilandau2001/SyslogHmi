using System;
using System.Globalization;
using System.Windows.Data;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// Converts IsAsking boolean to button content text.
    /// </summary>
    public class IsAskingToButtonContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAsking)
            {
                return isAsking ? "⟳ Asking..." : "Ask IA";
            }
            return "Ask IA";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
