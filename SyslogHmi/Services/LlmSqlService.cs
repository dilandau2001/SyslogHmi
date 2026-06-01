using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LLama;
using LLama.Common;

namespace SyslogHmi.Services
{
    // Added IDisposable to safely release LLM unmanaged memory
    // Now supports on-demand model loading from downloaded sources
    public class LlmSqlService : IDisposable
    {
        private LLamaWeights _model;
        private LLamaContext _context;
        private InteractiveExecutor _executor;
        private ChatSession _session;
        private bool _isInitialized;
        private string _loadedModelPath;

        /// <summary>
        /// Creates a new LLM SQL service with a specific model path.
        /// The model file must already exist at the specified path.
        /// </summary>
        public LlmSqlService()
        {
            _isInitialized = false;
            _loadedModelPath = null;
        }

        /// <summary>
        /// Gets a value indicating whether the LLM model is loaded and ready for inference.
        /// </summary>
        public bool Initialized => _isInitialized;

        /// <summary>
        /// Gets the path of the currently loaded model, or null if no model is loaded.
        /// </summary>
        public string LoadedModelPath => _loadedModelPath;

        /// <summary>
        /// Initializes the LLM model. Must be called before TranslateToSqlAsync.
        /// This is separated from the constructor to allow for lazy initialization
        /// and proper error handling during model loading.
        /// </summary>
        public void LoadModel(string modelPath)
        {
            if (_isInitialized)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[LlmSqlService] Initializing with model: {modelPath}");

                if (!File.Exists(modelPath))
                {
                    _isInitialized = false;
                    System.Diagnostics.Debug.WriteLine($"No model found in that path: {modelPath}");
                    return;
                }

                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = 2048,
                    GpuLayerCount = 0 // Set to > 0 if you have a CUDA-compatible GPU
                };

                // Load model and initialize components
                _model = LLamaWeights.LoadFromFile(parameters);
                _context = _model.CreateContext(parameters);
                _executor = new InteractiveExecutor(_context);
                _session = new ChatSession(_executor);

                _isInitialized = true;
                _loadedModelPath = modelPath;
                System.Diagnostics.Debug.WriteLine("[LlmSqlService] Initialization complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LlmSqlService] Initialization failed: {ex.Message}");
                throw;
            }
        }

        public void UnloadModel()
        {
            _session = null;
            _executor = null;

            _context?.Dispose();
            _context = null;

            _model?.Dispose();
            _model = null;

            _isInitialized = false;
            _loadedModelPath = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();

            System.Diagnostics.Debug.WriteLine(
                "[LlmSqlService] Model unloaded");
        }

        public async Task<string> TranslateToSqlAsync(string userInput)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("LlmSqlService must be initialized by calling Initialize() first");

            // Clear entire history so previous translations don't bleed into the next one
            _session.History.Messages.Clear();

            // Re-inject the System Prompt freshly every time
            _session.History.AddMessage(AuthorRole.System, IAModel.Constants.SystemPrompt);

            // Setup the inference settings to stop the model from talking too much
            var inferenceParams = new InferenceParams
            {
                MaxTokens = 256,
                AntiPrompts = ["User:", "\n"] // Force stops if it tries to generate user text
            };

            var sb = new StringBuilder();

            // Pass the historical system prompt + the new user input message properly
            var userMessage = new ChatHistory.Message(AuthorRole.User, userInput);

            await foreach (var token in _session.ChatAsync(userMessage, inferenceParams))
            {
                sb.Append(token);
            }

            return Clean(sb.ToString());
        }

        private static string Clean(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return string.Empty;

            // Strip Markdown formatting if the model slipped up
            sql = sql.Replace("```sql", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("```", "")
                     .Trim();

            // Locate the SELECT block 
            var selectIndex = sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
            if (selectIndex >= 0)
            {
                sql = sql[selectIndex..];
            }

            // Remove trailing semicolon if your execution engine doesn't like it, 
            // or leave it based on preference.
            return sql.Trim();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _model?.Dispose();
        }
    }
}


