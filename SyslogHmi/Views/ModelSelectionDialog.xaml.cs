using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SyslogHmi.Services;
using SyslogHmi.ViewModels;

namespace SyslogHmi.Views
{
    /// <summary>
    /// Dialog for selecting and managing AI models for SQL generation.
    /// Allows users to view available models, download new ones, and select which model to use.
    /// </summary>
    public partial class ModelSelectionDialog : Window
    {
        private ModelSelectionViewModel _viewModel;
        private readonly LlmSqlService _llmService;
        private readonly LlmModelManager _modelManager;

        public ModelSelectionDialog(
            LlmModelManager modelManager,
            LlmSqlService llmService)
        {
            InitializeComponent();
            _modelManager = modelManager;
            _llmService = llmService;
            _viewModel = new ModelSelectionViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// Gets the selected model that will be used for SQL generation.
        /// </summary>
        public LlmModel SelectedModel { get; private set; }

        /// <summary>
        /// Exposes the internal view model to the caller so they can set initial active model or read the prompt.
        /// </summary>
        public ModelSelectionViewModel ViewModel => _viewModel;

        /// <summary>
        /// Gets whether the dialog was closed with a valid model selection.
        /// </summary>
        public bool ModelSelected { get; private set; }

        /// <summary>
        /// The natural language prompt entered by the user in the dialog.
        /// </summary>
        public string SelectedPrompt => _viewModel?.PromptText;

        public string QueryResult { get; private set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to models loaded event
            _viewModel.ModelsLoaded += OnModelsLoaded;
            _viewModel?.LoadModels();
        }

        private void OnModelsLoaded()
        {
            // Now that models are loaded, sync the loaded model state from the service
            if (_llmService.Initialized && !string.IsNullOrEmpty(_llmService.LoadedModelPath))
            {
                var allModels = _viewModel.AllModels;
                var loadedModelFileName = System.IO.Path.GetFileName(_llmService.LoadedModelPath);

                var loadedModel = allModels.FirstOrDefault(m =>
                    m.FileName.Equals(loadedModelFileName, StringComparison.OrdinalIgnoreCase));

                if (loadedModel != null)
                {
                    _viewModel.LoadedModel = loadedModel;
                    System.Diagnostics.Debug.WriteLine($"[ModelSelectionDialog] Synced loaded model: {loadedModel.Name}");
                }
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var model = button?.DataContext as SyslogHmi.Services.LlmModel;
            if (model == null) return;

            _viewModel.SelectedModel = model;
            try
            {
                await _viewModel.DownloadSelectedModel();
                RefreshModelList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading model: {ex.Message}", "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var model = button?.DataContext as SyslogHmi.Services.LlmModel;
            if (model == null) return;

            try
            {
                _viewModel.IsLoadingModel = true;
                var modelPath = _modelManager.GetModelPath(model.FileName);

                // Load model on a background thread to not block UI
                await Task.Run(() =>
                {
                    // Unload current model if any
                    if (_llmService.Initialized)
                    {
                        _llmService.UnloadModel();
                    }

                    _llmService.LoadModel(modelPath);
                });

                _viewModel.LoadedModel = model;
                RefreshModelList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading model: {ex.Message}", "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _viewModel.IsLoadingModel = false;
            }
        }

        private void UnloadButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var model = button?.DataContext as SyslogHmi.Services.LlmModel;
            if (model == null) return;

            try
            {
                _llmService.UnloadModel();
                _viewModel.LoadedModel = null;
                RefreshModelList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error unloading model: {ex.Message}", "Unload Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var model = button?.DataContext as SyslogHmi.Services.LlmModel;
            if (model == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{model.Name}'?\nFile size: {model.FileSizeDisplay}",
                "Delete Model",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // If it's the loaded model, unload it first
                    if (_llmService.Initialized && _viewModel.LoadedModel?.FileName == model.FileName)
                    {
                        _llmService.UnloadModel();
                        _viewModel.LoadedModel = null;
                    }

                    // Delete the cached model file
                    _modelManager.ClearCachedModel(model.FileName);
                    RefreshModelList();

                    MessageBox.Show($"Model '{model.Name}' has been deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting model: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshModelList()
        {
            // Refresh the ListBox to update button visibility
            ModelsList.Items.Refresh();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ModelsLoaded -= OnModelsLoaded;
            }
            ModelSelected = false;
            DialogResult = false;
            Close();
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.CancelDownload();
        }

        private async void AskIa_Click(object sender, RoutedEventArgs e)
        {
            if (!_llmService.Initialized)
            {
                MessageBox.Show("You need to load a model first.");
                return;
            }

            try
            {
                _viewModel.IsAsking = true;
                var sql = await _llmService.TranslateToSqlAsync(SelectedPrompt);

                QueryResult = sql;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing request: {ex.Message}", "Request Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                _viewModel.IsAsking = false;
            }
        }
    }
}
