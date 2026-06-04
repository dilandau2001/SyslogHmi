using System.Windows.Input;
using SyslogHmi.ViewModels;

namespace SyslogHmi.Models
{
    /// <summary>
    /// Represents a model item in the model selection dialog with its state and available actions.
    /// </summary>
    public class ModelItemViewModel : ViewModelBase
    {
        private ModelState _state;
        private bool _isLoading;
        private string _statusMessage;

        public ModelItemViewModel(string modelName, ModelState initialState)
        {
            ModelName = modelName;
            _state = initialState;
            _isLoading = false;
            _statusMessage = string.Empty;
        }

        public string ModelName { get; }

        public ModelState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotLoaded));
                    OnPropertyChanged(nameof(IsLoaded));
                    OnPropertyChanged(nameof(BackgroundColor));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanPerformAction));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        // Computed properties for UI
        public bool IsDownloaded => State == ModelState.Downloaded || State == ModelState.Loaded;
        public bool IsLoaded => State == ModelState.Loaded;
        public bool IsNotLoaded => !IsLoaded;
        public bool CanPerformAction => !IsLoading;

        public string BackgroundColor => State switch
        {
            ModelState.Loaded => "#4CAF50", // Green
            ModelState.Downloaded => "#2196F3", // Blue
            _ => "#9E9E9E" // Gray
        };

        public ICommand DownloadCommand { get; set; }
        public ICommand LoadCommand { get; set; }
        public ICommand UnloadCommand { get; set; }
    }

    public enum ModelState
    {
        NotDownloaded,
        Downloaded,
        Loaded
    }
}
