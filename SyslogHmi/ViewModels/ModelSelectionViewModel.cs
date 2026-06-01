using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SyslogHmi.Services;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// ViewModel for the model selection dialog.
    /// Manages model selection, downloading, and progress reporting.
    /// </summary>
    public class ModelSelectionViewModel : ViewModelBase
    {
        private readonly LlmModelManager _modelManager;
        private readonly LlmSqlService _llmService;
        private ObservableCollection<LlmModel> _allModels;
        private LlmModel _selectedModel;
        private LlmModel _loadedModel;
        private bool _isDownloading;
        private bool _isLoadingModel;
        private bool _isAsking;
        private string _currentDownloadName;
        private double _downloadProgressPercent;
        private long _downloadedBytes;
        private long _totalBytes;
        private DateTime _downloadStartTime;
        private CancellationTokenSource _cancellationTokenSource;
        private string _promptText;



        public ModelSelectionViewModel()
        {
            _modelManager = new LlmModelManager();
            _llmService = new LlmSqlService();
            _allModels = [];

            // Subscribe to download events
            _modelManager.DownloadProgress += OnDownloadProgress;
            _modelManager.DownloadStarted += OnDownloadStarted;
            _modelManager.DownloadCompleted += OnDownloadCompleted;
            _modelManager.DownloadFailed += OnDownloadFailed;
        }

        public ObservableCollection<LlmModel> AllModels
        {
            get => _allModels;
            set
            {
                if (_allModels != value)
                {
                    _allModels = value;
                    OnPropertyChanged();
                }
            }
        }

        public LlmModel SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanUseModel));
                }
            }
        }

        public LlmModel LoadedModel
        {
            get => _loadedModel;
            set
            {
                if (_loadedModel != value)
                {
                    _loadedModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanAsk));
                }
            }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (_isDownloading != value)
                {
                    _isDownloading = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanUseModel => SelectedModel != null && !IsDownloading;

        public bool IsLoadingModel
        {
            get => _isLoadingModel;
            set
            {
                if (_isLoadingModel != value)
                {
                    _isLoadingModel = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAsking
        {
            get => _isAsking;
            set
            {
                if (_isAsking != value)
                {
                    _isAsking = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanAsk));
                }
            }
        }

        public bool CanAsk => LoadedModel != null && !IsAsking;

        public string PromptText
        {
            get => _promptText;
            set => SetProperty(ref _promptText, value);
        }

        public string CurrentDownloadName
        {
            get => _currentDownloadName;
            set
            {
                if (_currentDownloadName != value)
                {
                    _currentDownloadName = value;
                    OnPropertyChanged();
                }
            }
        }

        public double DownloadProgressPercent
        {
            get => _downloadProgressPercent;
            set
            {
                if (Math.Abs(_downloadProgressPercent - value) > 0.1)
                {
                    _downloadProgressPercent = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DownloadedMb
        {
            get => (_downloadedBytes / (1024.0 * 1024.0)).ToString("F1");
        }

        public string TotalMb
        {
            get => (_totalBytes / (1024.0 * 1024.0)).ToString("F1");
        }

        public string DownloadSpeed
        {
            get
            {
                if (_downloadStartTime == default || _downloadedBytes == 0)
                    return "calculating...";

                var elapsed = DateTime.Now - _downloadStartTime;
                if (elapsed.TotalSeconds < 1)
                    return "starting...";

                var bytesPerSecond = _downloadedBytes / elapsed.TotalSeconds;
                if (bytesPerSecond < 1024)
                    return $"{bytesPerSecond:F0} B/s";
                if (bytesPerSecond < 1024 * 1024)
                    return $"{bytesPerSecond / 1024:F1} KB/s";
                return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
            }
        }

        public string TimeRemaining
        {
            get
            {
                if (_downloadStartTime == default || _downloadedBytes == 0 || _totalBytes == 0)
                    return "calculating...";

                var elapsed = DateTime.Now - _downloadStartTime;
                if (elapsed.TotalSeconds < 1)
                    return "calculating...";

                var bytesPerSecond = _downloadedBytes / elapsed.TotalSeconds;
                if (bytesPerSecond == 0)
                    return "calculating...";

                var remainingBytes = _totalBytes - _downloadedBytes;
                var remainingSeconds = remainingBytes / bytesPerSecond;

                if (remainingSeconds < 60)
                    return $"{remainingSeconds:F0}s";
                if (remainingSeconds < 3600)
                    return $"{remainingSeconds / 60:F0}m";
                return $"{remainingSeconds / 3600:F1}h";
            }
        }

        /// <summary>
        /// Gets the state of a specific model (NotDownloaded, Downloaded, or Loaded).
        /// </summary>
        public ModelState GetModelState(LlmModel model)
        {
            if (model == null)
                return ModelState.NotDownloaded;

            if (LoadedModel?.FileName.Equals(model.FileName, StringComparison.OrdinalIgnoreCase) == true)
                return ModelState.Loaded;

            if (IsModelDownloaded(model.FileName))
                return ModelState.Downloaded;

            return ModelState.NotDownloaded;
        }

        public void LoadModels()
        {
            Task.Run(() =>
            {
                try
                {
                    var allModels = ModelCatalog.GetAvailableModels();

                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        AllModels.Clear();
                        foreach (var model in allModels)
                        {
                            AllModels.Add(model);
                        }

                        // Select first model by default
                        if (AllModels.Count > 0)
                        {
                            SelectedModel = AllModels[0];
                        }

                        Debug.WriteLine("[ModelSelectionViewModel] Models loaded");
                        ModelsLoaded?.Invoke();
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ModelSelectionViewModel] Failed to load models: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Event raised when models have been loaded and AllModels collection is populated.
        /// </summary>
        public event Action ModelsLoaded;

        public bool IsModelDownloaded(string fileName)
        {
            return _modelManager.IsModelCached(fileName);
        }

        public async Task DownloadSelectedModel()
        {
            if (SelectedModel == null)
                return;

            IsDownloading = true;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                CurrentDownloadName = SelectedModel.Name;
                _downloadStartTime = DateTime.Now;
                _downloadedBytes = 0;
                _totalBytes = SelectedModel.FileSizeBytes;

                var modelPath = await _modelManager.DownloadModelAsync(SelectedModel, _cancellationTokenSource.Token);

                Debug.WriteLine($"[ModelSelectionViewModel] Download completed: {modelPath}");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[ModelSelectionViewModel] Download cancelled by user");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelSelectionViewModel] Download failed: {ex.Message}");
                throw;
            }
            finally
            {
                IsDownloading = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        public void CancelDownload()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void OnDownloadProgress(long bytesDownloaded, long totalBytes)
        {
            _downloadedBytes = bytesDownloaded;
            _totalBytes = totalBytes;

            var percent = totalBytes > 0 ? (bytesDownloaded / (double)totalBytes) * 100 : 0;
            DownloadProgressPercent = percent;

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnPropertyChanged(nameof(DownloadedMb));
                OnPropertyChanged(nameof(TotalMb));
                OnPropertyChanged(nameof(DownloadSpeed));
                OnPropertyChanged(nameof(TimeRemaining));
            });
        }

        private void OnDownloadStarted(string modelName)
        {
            Debug.WriteLine($"[ModelSelectionViewModel] Download started: {modelName}");
        }

        private void OnDownloadCompleted(string modelPath)
        {
            Debug.WriteLine($"[ModelSelectionViewModel] Download completed: {modelPath}");
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsDownloading = false;
            });
        }

        private void OnDownloadFailed(Exception ex)
        {
            Debug.WriteLine($"[ModelSelectionViewModel] Download failed: {ex.Message}");
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsDownloading = false;
            });
        }

        // ViewModelBase already provides OnPropertyChanged and PropertyChanged event.
        // This method remains for backwards compatibility and will call base.OnPropertyChanged.
        protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Represents the state of a model.
    /// </summary>
    public enum ModelState
    {
        NotDownloaded,
        Downloaded,
        Loaded
    }
}
