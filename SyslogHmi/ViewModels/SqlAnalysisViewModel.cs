using Microsoft.Win32;
using SyslogHmi.Models;
using SyslogHmi.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// View model for the SQL Analysis view.
    /// Manages free-form SQL query execution, result display, and error handling.
    /// Provides a collection of messages from custom SQL queries for display in MessagesView.
    /// </summary>
    public class SqlAnalysisViewModel : ViewModelBase
    {
        /// <summary>
        /// Service for executing custom SQL queries against the database.
        /// </summary>
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// Collection storing the results of the executed query.
        /// </summary>
        public BulkObservableCollection<SyslogMessage> Messages { get; }

        /// <summary>
        /// Filtered collection displaying only messages that match current filter criteria.
        /// </summary>
        public BulkObservableCollection<SyslogMessage> FilteredMessages { get; }

        /// <summary>
        /// View model managing filter criteria for displayed query results.
        /// </summary>
        public FilterViewModel FilterViewModel { get; }

        /// <summary>
        /// View model managing color rules and formatting for message display.
        /// </summary>
        public ColorRuleViewModel ColorRuleViewModel { get; }

        /// <summary>
        /// Gets or sets the currently selected syslog message in the query results.
        /// When changed, also updates the SelectedMessageDetail property.
        /// </summary>
        public SyslogMessage SelectedMessage
        {
            get;
            set
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(SelectedMessageDetail));
            }
        }

        /// <summary>
        /// Gets the full message text of the currently selected syslog message.
        /// Returns an empty string if no message is selected.
        /// This property is read-only to avoid WPF binding write errors.
        /// </summary>
        public string SelectedMessageDetail => SelectedMessage?.Message ?? string.Empty;

        /// <summary>
        /// Gets or sets the SQL query text entered by the user.
        /// </summary>
        public string QueryText
        {
            get;
            set => SetProperty(ref field, value);
        } = "SELECT * FROM SyslogMessages LIMIT 100;";

        /// <summary>
        /// Gets or sets the status message displayed to the user (errors, success messages, etc.).
        /// </summary>
        public string StatusMessage
        {
            get;
            set => SetProperty(ref field, value);
        } = "Ready to execute SQL queries";

        /// <summary>
        /// Gets or sets the count of results returned from the last query execution.
        /// </summary>
        public int ResultCount
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether a query is currently being executed.
        /// </summary>
        public bool IsExecuting
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// Command to execute the SQL query and display results.
        /// </summary>
        public ICommand ExecuteQueryCommand { get; }

        /// <summary>
        /// Command to clear all results and reset the view to initial state.
        /// </summary>
        public ICommand ClearResultsCommand { get; }

        /// <summary>
        /// Command to reset the query text to a default SELECT example.
        /// </summary>
        public ICommand ResetQueryCommand { get; }

        /// <summary>
        /// Command to export the query results to a CSV or Excel file.
        /// </summary>
        public ICommand ExportResultsCommand { get; }

        /// <summary>
        /// Initializes a new instance of the SqlAnalysisViewModel class.
        /// Sets up collections, services, and commands for SQL query execution and analysis.
        /// </summary>
        public SqlAnalysisViewModel()
        {
            // Initialize collections
            Messages = new BulkObservableCollection<SyslogMessage>();
            FilteredMessages = new BulkObservableCollection<SyslogMessage>();
            FilterViewModel = new FilterViewModel();

            // Initialize database service
            _databaseService = new DatabaseService();

            // Initialize color rule view model to apply color rules to messages
            ColorRuleViewModel = new ColorRuleViewModel(_databaseService);

            // Configure sorting by timestamp descending
            CollectionViewSource.GetDefaultView(Messages).SortDescriptions.Add(new System.ComponentModel.SortDescription("Timestamp", System.ComponentModel.ListSortDirection.Descending));
            CollectionViewSource.GetDefaultView(FilteredMessages).SortDescriptions.Add(new System.ComponentModel.SortDescription("Timestamp", System.ComponentModel.ListSortDirection.Descending));

            // Initialize commands
            ExecuteQueryCommand = new RelayCommand(_ => ExecuteQuery(), _ => !IsExecuting);
            ClearResultsCommand = new RelayCommand(_ => ClearResults());
            ResetQueryCommand = new RelayCommand(_ => ResetQuery());
            ExportResultsCommand = new RelayCommand(_ => ExportResults(), _ => Messages.Count > 0);

            // Subscribe to filter changes to refresh filtered results
            FilterViewModel.FiltersChanged += (_, _) => System.Windows.Application.Current?.Dispatcher?.Invoke(RefreshFilteredMessages);

            // Subscribe to color rule changes to reapply colors to displayed messages
            ColorRuleViewModel.RuleChanged += (_, _) => System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                foreach (var syslogMessage in Messages)
                {
                    ApplyColorRule(syslogMessage);
                }
            });
        }

        /// <summary>
        /// Executes the SQL query entered by the user and displays results.
        /// Validates input, executes the query, and handles any errors.
        /// Updates ResultCount and displays status messages.
        /// </summary>
        private void ExecuteQuery()
        {
            if (string.IsNullOrWhiteSpace(QueryText))
            {
                StatusMessage = "Error: Query text cannot be empty";
                return;
            }

            IsExecuting = true;
            StatusMessage = "Executing query...";
            ResultCount = 0;

            try
            {
                // Execute the query through the database service
                var results = _databaseService.ExecuteCustomQuery(QueryText);

                // Clear existing results
                Messages.Clear();
                FilteredMessages.Clear();

                // Add new results to the collection
                if (results != null && results.Count > 0)
                {
                    // Apply color rules to each message before adding to collection
                    foreach (var message in results)
                    {
                        ApplyColorRule(message);
                    }

                    // Use PrependRange for consistency with MainViewModel (adds at beginning in reverse order)
                    Messages.PrependRange(results);
                    RefreshFilteredMessages();
                    ResultCount = results.Count;
                    StatusMessage = $"Query executed successfully. {ResultCount} rows returned.";
                }
                else
                {
                    ResultCount = 0;
                    StatusMessage = "Query executed successfully. No rows returned.";
                }
            }
            catch (InvalidOperationException ex)
            {
                // Handle query type validation errors (non-SELECT queries)
                StatusMessage = $"Query error: {ex.Message}";
                Debug.WriteLine($"Query execution error: {ex}");
            }
            catch (Exception ex)
            {
                // Handle general SQL errors
                StatusMessage = $"Database error: {ex.Message}";
                Debug.WriteLine($"Database error: {ex}");
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// Refreshes the filtered message collection based on current filter criteria.
        /// Clears and repopulates the filtered collection with matching messages.
        /// </summary>
        private void RefreshFilteredMessages()
        {
            FilteredMessages.Clear();
            var filtered = Messages.Where(m => FilterViewModel.MatchesFilters(m)).ToList();

            // Use range-based insertion for efficient bulk operation
            if (filtered.Count > 0)
            {
                FilteredMessages.PrependRange(filtered);
            }
        }

        /// <summary>
        /// Clears all query results and resets the UI to initial state.
        /// Clears both Messages and FilteredMessages collections.
        /// </summary>
        private void ClearResults()
        {
            Messages.Clear();
            FilteredMessages.Clear();
            ResultCount = 0;
            StatusMessage = "Results cleared. Ready for new query.";
        }

        /// <summary>
        /// Resets the query text to a default SELECT example.
        /// Useful for quickly starting a new analysis session.
        /// </summary>
        private void ResetQuery()
        {
            QueryText = "SELECT * FROM SyslogMessages LIMIT 100;";
            StatusMessage = "Query reset to default example.";
        }

        /// <summary>
        /// Exports the current query results to a CSV file using a file save dialog.
        /// Prompts the user to select a file location and format (CSV or CSV-as-Excel).
        /// </summary>
        private void ExportResults()
        {
            if (Messages.Count == 0)
            {
                StatusMessage = "Cannot export: no results to export";
                return;
            }

            // Use the native WPF SaveFileDialog
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Query Results",
                DefaultExt = ".csv",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"syslog_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            // Show the dialog (returns bool? in WPF)
            bool? result = saveDialog.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(saveDialog.FileName))
            {
                try
                {
                    var exportService = new ExportService();
                    exportService.ExportToCsv(Messages, saveDialog.FileName);

                    StatusMessage = $"Successfully exported {Messages.Count} messages to {saveDialog.FileName}";
                    System.Diagnostics.Debug.WriteLine($"Export completed: {saveDialog.FileName}");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export error: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"Export failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Applies the appropriate color rule to a syslog message based on its properties.
        /// Sets background color, foreground color, font weight, and font style.
        /// If no rule matches, applies default formatting (transparent background, black text, normal weight/style).
        /// </summary>
        /// <param name="message">The syslog message to format.</param>
        private void ApplyColorRule(SyslogMessage message)
        {
            try
            {
                // Find the first applicable color rule for this message
                var rule = ColorRuleViewModel?.GetApplicableRule(message);
                if (rule != null)
                {
                    message.BackgroundColor = rule.Format.BackgroundColor ?? "Transparent";
                    message.ForegroundColor = rule.Format.ForegroundColor ?? "Black";
                    message.FontWeight = rule.Format.IsBold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal;
                    message.FontStyle = rule.Format.IsItalic ? System.Windows.FontStyles.Italic : System.Windows.FontStyles.Normal;
                }
                else
                {
                    // Apply default formatting when no rule matches
                    message.BackgroundColor = "Transparent";
                    message.ForegroundColor = "Black";
                    message.FontWeight = System.Windows.FontWeights.Normal;
                    message.FontStyle = System.Windows.FontStyles.Normal;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying color rule: {ex}");
            }
        }

        /// <summary>
        /// Disposes of resources used by this view model.
        /// </summary>
        public void Cleanup()
        {
            _databaseService?.Dispose();
        }
    }
}
