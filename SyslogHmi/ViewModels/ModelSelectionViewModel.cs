using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SyslogHmi.Services;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// ViewModel for the LLM model selection dialog window.
    /// Manages local model catalog inventory selection, asynchronous file downloads, and data-bound progress computation.
    /// </summary>
    public class ModelSelectionViewModel : ViewModelBase
    {
        private readonly LlmModelManager _modelManager;
        private ObservableCollection<LlmModel> _allModels;
        private long _downloadedBytes;
        private long _totalBytes;
        private DateTime _downloadStartTime;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelSelectionViewModel"/> class.
        /// Hooks into backend model management events for handling progress updates.
        /// </summary>
        public ModelSelectionViewModel()
        {
            _modelManager = new LlmModelManager();
            _allModels = [];

            // Subscribe to background service down-stream download events
            _modelManager.DownloadProgress += OnDownloadProgress;
            _modelManager.DownloadStarted += OnDownloadStarted;
            _modelManager.DownloadCompleted += OnDownloadCompleted;
            _modelManager.DownloadFailed += OnDownloadFailed;
        }

        /// <summary>
        /// Gets or sets the absolute array list of all available LLM variations discovered in the system catalog.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the specific model target highlighted by the operator in the selection UI grid.
        /// Updates the execution validation flags automatically on change.
        /// </summary>
        public LlmModel SelectedModel
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanUseModel)); // Dependent flag: recalculate if selection is valid
                }
            }
        }

        /// <summary>
        /// Gets or sets the model binary that is currently instantiated and mapped into active CPU/GPU VRAM.
        /// </summary>
        public LlmModel LoadedModel
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanAsk)); // Dependent flag: recalculate if query loop can execute
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether an active file retrieval operation is processing over the network.
        /// </summary>
        public bool IsDownloading
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the currently selected model is valid for configuration use.
        /// Returns false if no model is selected or if a file download is already in progress.
        /// </summary>
        public bool CanUseModel => SelectedModel != null && !IsDownloading;

        /// <summary>
        /// Gets or sets a value indicating whether the application is loading the model weights into engine memory.
        /// </summary>
        public bool IsLoadingModel
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether an active inference question prompt is being processed by the local model.
        /// </summary>
        public bool IsAsking
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanAsk)); // Dependent flag: lock out simultaneous requests
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether an operator can submit a text prompt to the chat engine interface.
        /// </summary>
        public bool CanAsk => LoadedModel != null && !IsAsking;

        /// <summary>
        /// Gets or sets the raw string content of the prompt text input field.
        /// </summary>
        public string PromptText
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// Gets or sets the descriptive title of the file currently executing a network payload transfer.
        /// </summary>
        public string CurrentDownloadName
        {
            get;
            set
            {
                if (field == value)
                    return;
                
                field = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the percentage value of the current download operation (Range: 0.0 to 100.0).
        /// Implements a low-pass delta filter check to avoid thrashing data bindings for minor micro-shifts.
        /// </summary>
        public double DownloadProgressPercent
        {
            get;
            set
            {
                if (!(Math.Abs(field - value) > 0.1))
                    return;
                
                field = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the current data volume downloaded formatted as Megabytes (MB) to one decimal place.
        /// </summary>
        public string DownloadedMb => (_downloadedBytes / (1024.0 * 1024.0)).ToString("F1");

        /// <summary>
        /// Gets the total target file volume formatted as Megabytes (MB) to one decimal place.
        /// </summary>
        public string TotalMb => (_totalBytes / (1024.0 * 1024.0)).ToString("F1");

        /// <summary>
        /// Gets the calculated rolling network ingestion speed string, dynamically choosing human-readable unit formats.
        /// </summary>
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
                return bytesPerSecond switch
                {
                    < 1024 => $"{bytesPerSecond:F0} B/s",
                    < 1024 * 1024 => $"{bytesPerSecond / 1024:F1} KB/s",
                    _ => $"{bytesPerSecond / (1024 * 1024):F1} MB/s"
                };
            }
        }

        /// <summary>
        /// Gets the calculated estimated arrival time remaining based on historical transfer rate values.
        /// </summary>
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
        /// Cross-evaluates properties to check if a specific model target is NotDownloaded, Downloaded, or currently Loaded into active memory.
        /// </summary>
        /// <param name="model">The model item to analyze.</param>
        /// <returns>The calculated deployment initialization state enumeration matching the target asset entry.</returns>
        public ModelState GetModelState(LlmModel model)
        {
            if (model == null)
                return ModelState.NotDownloaded;

            if (LoadedModel?.FileName.Equals(model.FileName, StringComparison.OrdinalIgnoreCase) == true)
                return ModelState.Loaded;

            return IsModelDownloaded(model.FileName) ? ModelState.Downloaded : ModelState.NotDownloaded;
        }

        /// <summary>
        /// Queries the local file catalog asynchronously to find local variations, populating the collection via the UI Thread dispatcher.
        /// </summary>
        public void LoadModels()
        {
            Task.Run(() =>
            {
                try
                {
                    var allModels = ModelCatalog.GetAvailableModels();

                    // Marshal collection changes back onto the UI application thread to prevent cross-thread collection exceptions
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        AllModels.Clear();
                        foreach (var model in allModels)
                        {
                            AllModels.Add(model);
                        }

                        // Select the first model catalog index option by default if available
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
        /// Event fired whenever the backend model processing loops complete an inventory population scan sequence.
        /// </summary>
        public event Action ModelsLoaded;

        /// <summary>
        /// Checks whether a specific model configuration name resides entirely within the local disk space directory cache.
        /// </summary>
        public bool IsModelDownloaded(string fileName)
        {
            return _modelManager.IsModelCached(fileName);
        }

        /// <summary>
        /// Pulls down the chosen model file from the remote resource URL using cancellation injection handlers.
        /// </summary>
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

        /// <summary>
        /// Signals cancellation to the current remote file download stream.
        /// </summary>
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

            // Dispatch properties recalculation metrics onto the main layout thread to assure real-time UI text rendering
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

        /// <summary>
        /// Overrides property changed operations to bridge framework visibility loops safely.
        /// </summary>
        protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Represents the deployment lifecycle storage state of an LLM asset configuration.
    /// </summary>
    public enum ModelState
    {
        /// <summary>The asset file needs to be retrieved via the network downloader.</summary>
        NotDownloaded,
        /// <summary>The asset file exists locally on the disk cache layout but is not initialized into active memory.</summary>
        Downloaded,
        /// <summary>The asset file weights are parsed, processed, and loaded into active VRAM execution contexts.</summary>
        Loaded
    }
}