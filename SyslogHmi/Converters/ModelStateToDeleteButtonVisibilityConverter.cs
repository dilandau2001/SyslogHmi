using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SyslogHmi.ViewModels;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// MultiValueConverter to show/hide Delete button based on model state.
    /// Shows for Downloaded and Loaded states (inverse of NotDownloaded).
    /// </summary>
    public class ModelStateToDeleteButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is SyslogHmi.Services.LlmModel model && values[1] is ModelSelectionViewModel vm)
            {
                var state = vm.GetModelState(model);
                // Show delete button for Downloaded and Loaded states
                return state != ModelState.NotDownloaded ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
