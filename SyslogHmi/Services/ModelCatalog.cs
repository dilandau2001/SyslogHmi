using System;
using System.Collections.Generic;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Represents a single LLM model available for download.
    /// </summary>
    public class LlmModel
    {
        /// <summary>
        /// Display name of the model (e.g., "Qwen 1.5B Q4").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of what this model is good for.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Download URL for the model file.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Expected filename after download.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Size in bytes for display purposes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Recommended RAM (in GB) needed to run this model.
        /// </summary>
        public int RecommendedRamGb { get; set; }

        /// <summary>
        /// Whether this model is suitable for limited resources.
        /// </summary>
        public bool IsLightweight { get; set; }

        /// <summary>
        /// Estimated inference speed (tokens per second, approximately).
        /// </summary>
        public string InferenceSpeed { get; set; }

        /// <summary>
        /// Returns human-readable file size.
        /// </summary>
        public string FileSizeDisplay
        {
            get
            {
                const long gb = 1024 * 1024 * 1024;
                const long mb = 1024 * 1024;

                if (FileSizeBytes >= gb)
                    return $"{FileSizeBytes / (double)gb:F2} GB";
                if (FileSizeBytes >= mb)
                    return $"{FileSizeBytes / (double)mb:F2} MB";
                return $"{FileSizeBytes / 1024.0:F2} KB";
            }
        }
    }

    /// <summary>
    /// Catalog of available LLM models that can be downloaded on-demand.
    /// </summary>
    public static class ModelCatalog
    {
        /// <summary>
        /// Gets all available models.
        /// Models are hosted on HuggingFace for reliability and accessibility.
        /// </summary>
        public static List<LlmModel> GetAvailableModels()
        {
            return
            [
                new LlmModel
                {
                    Name = "Qwen 1.5B Q4 (Recommended for Limited Resources)",
                    Description =
                        "Lightweight 1.5B parameter model optimized for SQL generation. Fast and memory efficient.",
                    DownloadUrl =
                        "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf",
                    FileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf",
                    FileSizeBytes = 990000000, // ~990 MB
                    RecommendedRamGb = 4,
                    IsLightweight = true,
                    InferenceSpeed = "~50-100 tokens/sec"
                },

                new LlmModel
                {
                    Name = "Mistral 7B Q4 (Recommended for Better Quality)",
                    Description = "More capable 7B model with better SQL generation quality. Requires more resources.",
                    DownloadUrl =
                        "https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF/resolve/main/mistral-7b-instruct-v0.2.Q4_K_M.gguf",
                    FileName = "mistral-7b-instruct-v0.2.Q4_K_M.gguf",
                    FileSizeBytes = 4500000000, // ~4.5 GB
                    RecommendedRamGb = 8,
                    IsLightweight = false,
                    InferenceSpeed = "~30-50 tokens/sec"
                },

                new LlmModel
                {
                    Name = "Phi 2.7B Q4 (Balanced Option)",
                    Description = "Good balance between speed and quality. Suitable for most systems.",
                    DownloadUrl = "https://huggingface.co/TheBloke/phi-2-GGUF/resolve/main/phi-2.Q4_K_M.gguf",
                    FileName = "phi-2.Q4_K_M.gguf",
                    FileSizeBytes = 1600000000, // ~1.6 GB
                    RecommendedRamGb = 6,
                    IsLightweight = true,
                    InferenceSpeed = "~40-80 tokens/sec"
                },

                new LlmModel
                {
                    Name = "Zephyr 7B Q4 (SQL & Code Focus)",
                    Description =
                        "Specialized variant for SQL generation and code tasks. Better at understanding database structures and generating optimized queries.",
                    DownloadUrl =
                        "https://huggingface.co/TheBloke/zephyr-7B-beta-GGUF/resolve/main/zephyr-7b-beta.Q4_K_M.gguf",
                    FileName = "zephyr-7b-beta.Q4_K_M.gguf",
                    FileSizeBytes = 1050000000, // ~1.05 GB
                    RecommendedRamGb = 6,
                    IsLightweight = false,
                    InferenceSpeed = "~45-80 tokens/sec"
                },

                new LlmModel
                {
                    Name = "OpenChat 3.5 Q4 (SQL Optimized)",
                    Description =
                        "Optimized for code and SQL generation. Lightweight yet capable at database queries and schema understanding.",
                    DownloadUrl =
                        "https://huggingface.co/TheBloke/openchat-3.5-GGUF/resolve/main/openchat-3.5.Q4_K_M.gguf",
                    FileName = "openchat-3.5.Q4_K_M.gguf",
                    FileSizeBytes = 1200000000, // ~1.2 GB
                    RecommendedRamGb = 6,
                    IsLightweight = false,
                    InferenceSpeed = "~50-85 tokens/sec"
                },

                new LlmModel
                {
                    Name = "Mistral Nemo 12B Q4 (Advanced SQL)",
                    Description =
                        "More advanced model for complex SQL generation. Better query optimization and schema comprehension. Requires more resources.",
                    DownloadUrl =
                        "https://huggingface.co/TheBloke/Mistral-Nemo-12B-Instruct-2407-GGUF/resolve/main/Mistral-Nemo-12B-Instruct-2407.Q4_K_M.gguf",
                    FileName = "mistral-nemo-12b-instruct.Q4_K_M.gguf",
                    FileSizeBytes = 1350000000, // ~1.35 GB
                    RecommendedRamGb = 8,
                    IsLightweight = false,
                    InferenceSpeed = "~35-65 tokens/sec"
                }
            ];
        }

        /// <summary>
        /// Gets the default/recommended model for new users.
        /// </summary>
        public static LlmModel GetDefaultModel()
        {
            return GetAvailableModels()[0]; // Qwen 1.5B
        }

        /// <summary>
        /// Gets a model by its filename.
        /// </summary>
        public static LlmModel GetModelByFileName(string fileName)
        {
            return GetAvailableModels().Find(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
