using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Manages LLM model downloads and local caching.
    /// Handles downloading models from remote sources and storing them locally.
    /// </summary>
    public class LlmModelManager
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _modelsDirectory;

        /// <summary>
        /// Event raised to report download progress.
        /// Arguments: (bytesDownloaded, totalBytes)
        /// </summary>
        public event Action<long, long> DownloadProgress;

        /// <summary>
        /// Event raised when download starts.
        /// Arguments: modelName
        /// </summary>
        public event Action<string> DownloadStarted;

        /// <summary>
        /// Event raised when download completes.
        /// Arguments: modelPath
        /// </summary>
        public event Action<string> DownloadCompleted;

        /// <summary>
        /// Event raised if download fails.
        /// Arguments: exception
        /// </summary>
        public event Action<Exception> DownloadFailed;

        public LlmModelManager()
        {
            // Initialize models directory in AppData
            _modelsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SyslogHmi",
                "Models");

            // Create directory if it doesn't exist
            Directory.CreateDirectory(_modelsDirectory);
        }

        /// <summary>
        /// Gets the full path where a model file should be stored.
        /// </summary>
        public string GetModelPath(string fileName)
        {
            return Path.Combine(_modelsDirectory, fileName);
        }

        /// <summary>
        /// Checks if a model is already cached locally.
        /// </summary>
        public bool IsModelCached(string fileName)
        {
            var path = GetModelPath(fileName);
            return File.Exists(path);
        }

        /// <summary>
        /// Downloads a model from the specified URL if not already cached.
        /// Reports progress through events.
        /// </summary>
        /// <param name="model">The LLM model to download.</param>
        /// <param name="cancellationToken">Token for canceling the download.</param>
        /// <returns>The local path to the downloaded model file.</returns>
        public async Task<string> DownloadModelAsync(LlmModel model, CancellationToken cancellationToken = default)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var modelPath = GetModelPath(model.FileName);

            // If already cached, return immediately
            if (File.Exists(modelPath))
            {
                System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Model already cached: {modelPath}");
                return modelPath;
            }

            try
            {
                DownloadStarted?.Invoke(model.Name);
                System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Starting download of {model.Name} from {model.DownloadUrl}");

                // Configure HTTP client for large file downloads
                _httpClient.Timeout = TimeSpan.FromHours(2);

                using var response = await _httpClient.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var canReportProgress = totalBytes != -1;

                // Download to temporary file first
                var tempPath = modelPath + ".tmp";

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
                {
                    var totalRead = 0L;
                    var buffer = new byte[8192];
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                        totalRead += read;

                        if (canReportProgress)
                        {
                            DownloadProgress?.Invoke(totalRead, totalBytes);
                        }
                    }
                }

                // Move temp file to final location
                if (File.Exists(modelPath))
                    File.Delete(modelPath);

                File.Move(tempPath, modelPath);

                System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Download completed: {modelPath}");
                DownloadCompleted?.Invoke(modelPath);

                return modelPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Download failed: {ex.Message}");
                DownloadFailed?.Invoke(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the directory where models are cached.
        /// </summary>
        public string GetModelsDirectory()
        {
            return _modelsDirectory;
        }

        /// <summary>
        /// Gets the total size of all cached models.
        /// </summary>
        public long GetCachedModelsSize()
        {
            long totalSize = 0;
            if (Directory.Exists(_modelsDirectory))
            {
                foreach (var file in Directory.GetFiles(_modelsDirectory, "*.gguf"))
                {
                    var info = new FileInfo(file);
                    totalSize += info.Length;
                }
            }
            return totalSize;
        }

        /// <summary>
        /// Clears a specific cached model file.
        /// </summary>
        public void ClearCachedModel(string fileName)
        {
            var path = GetModelPath(fileName);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Deleted cached model: {path}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Failed to delete model: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Clears all cached models.
        /// </summary>
        public void ClearAllCachedModels()
        {
            if (Directory.Exists(_modelsDirectory))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_modelsDirectory, "*.gguf"))
                    {
                        File.Delete(file);
                    }
                    System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Cleared all cached models");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LlmModelManager] Failed to clear cache: {ex.Message}");
                }
            }
        }
    }
}
