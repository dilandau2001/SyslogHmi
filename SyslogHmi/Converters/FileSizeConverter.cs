using System;
using System.Globalization;
using System.Windows.Data;

namespace SyslogHmi.Converters;

/// <summary>
/// Converts file size in bytes to human-readable format.
/// </summary>
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            const long kb = 1024;
            const long mb = 1024 * 1024;
            const long gb = 1024 * 1024 * 1024;

            if (bytes >= gb)
                return $"{bytes / (double)gb:F2} GB";
            if (bytes >= mb)
                return $"{bytes / (double)mb:F2} MB";
            if (bytes >= kb)
                return $"{bytes / (double)kb:F2} KB";
            return $"{bytes} B";
        }
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}