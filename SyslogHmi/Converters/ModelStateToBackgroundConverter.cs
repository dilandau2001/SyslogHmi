using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SyslogHmi.ViewModels;

namespace SyslogHmi.Converters
{
    /// <summary>
    /// MultiValueConverter that converts model and viewmodel to background color based on model state.
    /// Bindings: {Binding}, {Binding DataContext, RelativeSource=Window}
    /// </summary>
    public class ModelStateToBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] is the model (LlmModel)
            // values[1] is the ViewModel (ModelSelectionViewModel)
            if (values.Length >= 2 && values[0] is SyslogHmi.Services.LlmModel model && values[1] is ModelSelectionViewModel vm)
            {
                var state = vm.GetModelState(model);
                return state switch
                {
                    ModelState.NotDownloaded => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                    ModelState.Downloaded => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD")),
                    ModelState.Loaded => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")),
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"))
                };
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// MultiValueConverter to show/hide Download button based on model state.
    /// </summary>
    public class ModelStateToDownloadButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is SyslogHmi.Services.LlmModel model && values[1] is ModelSelectionViewModel vm)
            {
                var state = vm.GetModelState(model);
                return state == ModelState.NotDownloaded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// MultiValueConverter to show/hide Load button based on model state.
    /// </summary>
    public class ModelStateToLoadButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is SyslogHmi.Services.LlmModel model && values[1] is ModelSelectionViewModel vm)
            {
                var state = vm.GetModelState(model);
                return state == ModelState.Downloaded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// MultiValueConverter to show/hide Unload button based on model state.
    /// </summary>
    public class ModelStateToUnloadButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is SyslogHmi.Services.LlmModel model && values[1] is ModelSelectionViewModel vm)
            {
                var state = vm.GetModelState(model);
                return state == ModelState.Loaded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
