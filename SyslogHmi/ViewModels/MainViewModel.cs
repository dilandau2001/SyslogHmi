using SyslogHmi.Models;
using SyslogHmi.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// Main view model for the Syslog HMI application.
    /// Manages syslog message collection, filtering, color rules, and network listener coordination.
    /// Implements MVVM pattern with optimized batch processing for high-volume message handling.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Network listener for TCP and UDP syslog messages.
        /// </summary>
        private readonly SyslogListener _syslogListener;

        /// <summary>
        /// Database service for persisting syslog messages and color rules.
        /// </summary>
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// Service for sending random syslog messages for testing purposes.
        /// </summary>
        private readonly SyslogMessageSender _syslogMessageSender;

        /// <summary>
        /// Queue manager for batching incoming messages before UI update.
        /// </summary>
        private readonly SyslogQueueManager _queueManager;

        /// <summary>
        /// Timer that processes UI batches at regular intervals to prevent UI freezing.
        /// </summary>
        private readonly DispatcherTimer _uiRefreshTimer;

        /// <summary>
        /// Maximum number of messages to keep in the UI collection before removing the oldest entries.
        /// </summary>
        private const int MaxUiMessages = 99999;

        /// <summary>
        /// Total count of messages received from all sources.
        /// </summary>
        private int _totalMessagesReceived;

        /// <summary>
        /// Optimized collection for bulk message insertion operations.
        /// Provides efficient batch handling for high-volume message reception.
        /// </summary>
        public BulkObservableCollection<SyslogMessage> Messages { get; }

        /// <summary>
        /// Filtered collection displaying only messages that match active filter criteria.
        /// Updates dynamically when filter parameters change.
        /// </summary>
        public BulkObservableCollection<SyslogMessage> FilteredMessages { get; }

        /// <summary>
        /// View model managing filter criteria (severity, hostname, priority, etc.).
        /// </summary>
        public FilterViewModel FilterViewModel { get; }

        /// <summary>
        /// View model managing color rules and formatting for message display.
        /// </summary>
        public ColorRuleViewModel ColorRuleViewModel { get; }

        /// <summary>
        /// View model managing SQL analysis queries and custom query result display.
        /// Provides access to the SQL Analysis tab functionality.
        /// </summary>
        public SqlAnalysisViewModel SqlAnalysisViewModel { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the syslog listener is currently active.
        /// </summary>
        public bool IsListening
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// Gets or sets the total number of syslog messages received since application start.
        /// </summary>
        public int TotalMessagesReceived
        {
            get => _totalMessagesReceived;
            set => SetProperty(ref _totalMessagesReceived, value);
        }

        /// <summary>
        /// Gets or sets the current application status message displayed to the user.
        /// </summary>
        public string StatusMessage
        {
            get;
            set => SetProperty(ref field, value);
        } = "Ready";

        /// <summary>
        /// Gets or sets the TCP port number for listening to syslog messages.
        /// Default value is 601.
        /// </summary>
        public int SelectedTcpPort
        {
            get;
            set => SetProperty(ref field, value);
        } = 601;

        /// <summary>
        /// Gets or sets the UDP port number for listening to syslog messages.
        /// Default value is 514 (standard syslog port).
        /// </summary>
        public int SelectedUdpPort
        {
            get;
            set => SetProperty(ref field, value);
        } = 514;

        /// <summary>
        /// Gets or sets the currently selected syslog message in the message list.
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
        /// Command to start listening for syslog messages.
        /// Executes only when the listener is not currently active.
        /// </summary>
        public ICommand StartListeningCommand { get; }

        /// <summary>
        /// Command to stop listening for syslog messages.
        /// Executes only when the listener is currently active.
        /// </summary>
        public ICommand StopListeningCommand { get; }

        /// <summary>
        /// Command to clear all messages from the message collection and database.
        /// </summary>
        public ICommand ClearMessagesCommand { get; }

        /// <summary>
        /// Command to exit the application gracefully, stopping services and saving state.
        /// </summary>
        public ICommand ExitCommand { get; }

        /// <summary>
        /// Command to send a random syslog message for testing purposes.
        /// Sends 100 messages in rapid succession.
        /// </summary>
        public ICommand SendRandomSyslogCommand { get; }

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// Sets up collections, services, commands, and loads initial message history from the database.
        /// In design mode, skips all initialization for preview support.
        /// </summary>
        public MainViewModel()
        {
            // Check if running in design mode (Visual Studio designer or Blend)
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                // Optional: Load mock data for better preview in designer
                return;
            }

            // Step 1: Initialize all in-memory collections (clean slate)
            Messages = new BulkObservableCollection<SyslogMessage>();
            FilteredMessages = new BulkObservableCollection<SyslogMessage>();
            FilterViewModel = new FilterViewModel();

            // Configure default sorting by timestamp in descending order
            CollectionViewSource.GetDefaultView(Messages).SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
            CollectionViewSource.GetDefaultView(FilteredMessages).SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));

            // Step 2: Initialize data services
            _databaseService = new DatabaseService();
            _syslogMessageSender = new SyslogMessageSender();

            // Step 3: Load color rules (now that services are ready)
            ColorRuleViewModel = new ColorRuleViewModel(_databaseService);

            // Initialize SQL Analysis view model
            SqlAnalysisViewModel = new SqlAnalysisViewModel();

            // Step 4: Configure network listener and commands
            _syslogListener = new SyslogListener();
            _queueManager = new SyslogQueueManager(_databaseService);
            _syslogListener.MessageReceived += _queueManager.EnqueueMessage;

            // Initialize relay commands with enable/disable predicates
            StartListeningCommand = new RelayCommand(_ => StartListening(), _ => !IsListening);
            StopListeningCommand = new RelayCommand(_ => StopListening(), _ => IsListening);
            ClearMessagesCommand = new RelayCommand(_ => ClearMessages());
            ExitCommand = new RelayCommand(_ => Exit());
            SendRandomSyslogCommand = new RelayCommand(_ => SendRandomSyslogMessage());

            // Step 5: Configure UI refresh timer
            _uiRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _uiRefreshTimer.Tick += OnUiRefreshTimerTick;

            // Subscribe to filter and color rule changes
            FilterViewModel.FiltersChanged += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(RefreshFilteredMessages);
            ColorRuleViewModel.RuleChanged += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var syslogMessage in Messages)
                {
                    ApplyColorRule(syslogMessage);
                }
            });

            // Step 6: Load historical messages at the end
            // Use "Loaded" priority to ensure XAML bindings and converters are ready
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(LoadInitialHistory), DispatcherPriority.Loaded);

            StatusMessage = "Ready to start listening for syslog messages";
        }

        /// <summary>
        /// Loads the initial message history from the database.
        /// Applies color rules to all historical messages before displaying them.
        /// Called asynchronously after the UI is fully loaded.
        /// </summary>
        private void LoadInitialHistory()
        {
            try
            {
                // Retrieve the last N messages from the database
                var history = _databaseService.GetLastMessages(MaxUiMessages);

                // Apply color formatting rules to all historical messages
                foreach (var msg in history)
                {
                    ApplyColorRule(msg);
                }

                // Insert all historical messages at once without freezing the UI
                Messages.PrependRange(history);
                RefreshFilteredMessages();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading initial history: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts listening for syslog messages on the specified TCP and UDP ports.
        /// Activates the message queue manager and starts the UI refresh timer.
        /// Updates status message to reflect active listening state.
        /// </summary>
        private void StartListening()
        {
            try
            {
                _queueManager.Start();
                _syslogListener.Start(SelectedTcpPort, SelectedUdpPort);
                IsListening = true;
                StatusMessage = $"Listening on TCP:{SelectedTcpPort} and UDP:{SelectedUdpPort}";
                _uiRefreshTimer.Start();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Stops listening for syslog messages.
        /// Halts the network listener and queue manager, stops the UI refresh timer.
        /// Updates status message to reflect inactive state.
        /// </summary>
        private void StopListening()
        {
            _syslogListener.Stop();
            _queueManager.Stop();
            IsListening = false;
            _uiRefreshTimer.Stop();
            StatusMessage = "Stopped listening";
        }

        /// <summary>
        /// Processes batched messages from the queue and updates the UI collections.
        /// Applies color rules, filters messages, and manages memory by removing old entries.
        /// Called by the dispatcher timer at regular intervals to prevent UI freezing.
        /// Batch processing prevents excessive property change notifications.
        /// </summary>
        private void OnUiRefreshTimerTick(object sender, EventArgs e)
        {
            // Extract the current batch of messages from the queue
            var uiBatch = _queueManager.DequeueUiBatch();
            if (uiBatch.Count == 0) return;

            var filteredBatch = new List<SyslogMessage>();

            // Process colors and filtering in memory before touching WPF collections
            foreach (var message in uiBatch)
            {
                ApplyColorRule(message);
                _totalMessagesReceived++; // Increment total message counter

                // Check if message matches active filter criteria
                if (FilterViewModel.MatchesFilters(message))
                {
                    filteredBatch.Add(message);
                }
            }

            // Insert the complete batch while suppressing intermediate events
            // Critical optimization: prevents UI freezing and collection change notifications
            Messages.PrependRange(uiBatch);

            // Insert filtered messages if any match the criteria
            if (filteredBatch.Count > 0)
            {
                FilteredMessages.PrependRange(filteredBatch);
            }

            // Safely manage memory by removing oldest messages if collection exceeds limit
            if (Messages.Count > MaxUiMessages)
            {
                int excess = Messages.Count - MaxUiMessages;
                Messages.RemoveFromEnd(excess);
            }

            if (FilteredMessages.Count > MaxUiMessages)
            {
                int excess = FilteredMessages.Count - MaxUiMessages;
                FilteredMessages.RemoveFromEnd(excess);
            }

            // Notify UI of total message count change once per timer tick
            OnPropertyChanged(nameof(TotalMessagesReceived));
        }

        /// <summary>
        /// Refreshes the filtered message collection based on current filter criteria.
        /// Clears the existing filtered collection and repopulates with matching messages.
        /// Called when filter parameters change or when forced refresh is needed.
        /// </summary>
        private void RefreshFilteredMessages()
        {
            FilteredMessages.Clear();
            var filtered = Messages.Where(m => FilterViewModel.MatchesFilters(m)).ToList();

            // Use range-based insertion for efficient bulk operation
            FilteredMessages.PrependRange(filtered);
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
                Debug.WriteLine($"Error applying color rule: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all messages from the message collections and deletes them from the database.
        /// Resets the total message counter and clears any pending queue items.
        /// Updates status message to confirm operation completion.
        /// </summary>
        private void ClearMessages()
        {
            _queueManager.ClearPendingQueues();
            Messages.Clear();
            FilteredMessages.Clear();
            TotalMessagesReceived = 0;
            _databaseService.DeleteAllMessages();
            StatusMessage = "Messages cleared";
        }

        /// <summary>
        /// Sends 100 random syslog messages to the specified UDP port for testing purposes.
        /// Runs asynchronously to prevent UI blocking.
        /// Updates status message with success or error information.
        /// </summary>
        private void SendRandomSyslogMessage()
        {
            Task.Run(() =>
            {
                int count = 100;
                while (count > 0)
                {
                    try
                    {
                        _syslogMessageSender.SendRandomSyslogMessage("localhost", SelectedUdpPort);
                        StatusMessage = "Random syslog message sent";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Error sending message: {ex.Message}";
                    }

                    count--;
                }
            });
        }

        /// <summary>
        /// Gracefully shuts down the application.
        /// Stops the network listener, disposes database resources, and exits.
        /// </summary>
        private void Exit()
        {
            StopListening();
            _databaseService?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }
}