using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SyslogHmi.Models;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Service for exporting syslog messages to various file formats (CSV, Excel).
    /// Provides methods for serializing message collections to disk in standard formats.
    /// </summary>
    public sealed class ExportService
    {
        /// <summary>
        /// Exports a collection of syslog messages to a CSV file.
        /// Includes all message properties as columns with proper escaping for special characters.
        /// </summary>
        /// <param name="messages">The collection of messages to export.</param>
        /// <param name="filePath">The full file path where the CSV should be written.</param>
        /// <exception cref="ArgumentNullException">Thrown if messages or filePath is null.</exception>
        /// <exception cref="IOException">Thrown if there is an error writing to the file.</exception>
        public void ExportToCsv(IEnumerable<SyslogMessage> messages, string filePath)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write header row with all column names
                    WriteHeaderRow(writer);

                    // Write data rows
                    foreach (var message in messages)
                    {
                        WriteMessageRow(writer, message);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to export messages to CSV: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Exports a collection of syslog messages to an Excel-compatible CSV file (.xlsx via CSV).
        /// This creates a CSV file that Excel can open with proper formatting.
        /// For true .xlsx support, a library like EPPlus would be needed.
        /// </summary>
        /// <param name="messages">The collection of messages to export.</param>
        /// <param name="filePath">The full file path where the CSV should be written.</param>
        /// <exception cref="ArgumentNullException">Thrown if messages or filePath is null.</exception>
        /// <exception cref="IOException">Thrown if there is an error writing to the file.</exception>
        public void ExportToExcel(IEnumerable<SyslogMessage> messages, string filePath)
        {
            // For now, implement Excel export as CSV with .xlsx extension compatibility
            // Excel can open CSV files, and this avoids additional dependencies
            if (!filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                // Replace extension with .csv for proper handling
                var directory = Path.GetDirectoryName(filePath);
                var filename = Path.GetFileNameWithoutExtension(filePath);
                filePath = Path.Combine(directory, filename + ".csv");
            }

            ExportToCsv(messages, filePath);
        }

        /// <summary>
        /// Writes the CSV header row with all SyslogMessage property names.
        /// </summary>
        private void WriteHeaderRow(StreamWriter writer)
        {
            var headers = new[]
            {
                "Id",
                "Timestamp",
                "Hostname",
                "AppName",
                "ProcessId",
                "MessageId",
                "Severity",
                "Facility",
                "SeverityName",
                "FacilityName",
                "Message",
                "FullMessage",
                "ReceivedTime"
            };

            writer.WriteLine(string.Join(",", QuoteFields(headers)));
        }

        /// <summary>
        /// Writes a single syslog message as a CSV row.
        /// Properly escapes and quotes fields containing commas, quotes, or newlines.
        /// </summary>
        private void WriteMessageRow(StreamWriter writer, SyslogMessage message)
        {
            var fields = new[]
            {
                message.Id.ToString(),
                message.Timestamp.ToString("o"),  // ISO 8601 format
                message.Hostname ?? "",
                message.AppName ?? "",
                message.ProcessId.ToString(),
                message.MessageId ?? "",
                message.Severity.ToString(),
                message.Facility.ToString(),
                message.SeverityName ?? "",
                message.FacilityName ?? "",
                message.Message ?? "",
                message.FullMessage ?? "",
                message.ReceivedTime.ToString("o")  // ISO 8601 format
            };

            writer.WriteLine(string.Join(",", QuoteFields(fields)));
        }

        /// <summary>
        /// Quotes and escapes CSV fields.
        /// Fields are quoted if they contain commas, quotes, or newlines.
        /// Quotes within fields are escaped by doubling them.
        /// </summary>
        private string[] QuoteFields(string[] fields)
        {
            var quoted = new string[fields.Length];

            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i] ?? string.Empty;

                // Check if field needs quoting (contains comma, quote, newline, or carriage return)
                if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
                {
                    // Escape quotes by doubling them, then wrap in quotes
                    field = "\"" + field.Replace("\"", "\"\"") + "\"";
                }

                quoted[i] = field;
            }

            return quoted;
        }
    }
}
