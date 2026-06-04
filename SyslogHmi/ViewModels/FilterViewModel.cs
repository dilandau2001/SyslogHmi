using System;
using SyslogHmi.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// Manages the visual state and business rules for filtering Syslog streams.
    /// Exposes text match criteria, severity selections, timestamp ranges, and evaluates individual log entry validity.
    /// </summary>
    public class FilterViewModel : ViewModelBase
    {
        /// <summary>
        /// Gets or sets the target string matching rules for log hostnames.
        /// Supports multi-line input to track multiple hosts simultaneously.
        /// </summary>
        public string HostnameFilter
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        /// <summary>
        /// Gets or sets the target string matching rules for log application identifiers.
        /// Supports multi-line input.
        /// </summary>
        public string AppNameFilter
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        /// <summary>
        /// Gets or sets the text criteria to look for inside the core body contents of a Syslog entry.
        /// Supports multi-line input.
        /// </summary>
        public string MessageFilter
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        /// <summary>
        /// Gets or sets a single selected priority value fallback filter.
        /// </summary>
        public int? SelectedSeverity
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// Gets or sets the lower boundary date limit for log creation timestamps.
        /// </summary>
        public DateTime? StartTime
        {
            get;
            set
            {
                SetProperty(ref field, value);
                ValidateTimeRange();
            }
        } = null;

        /// <summary>
        /// Gets or sets the upper boundary date limit for log creation timestamps.
        /// </summary>
        public DateTime? EndTime
        {
            get;
            set
            {
                SetProperty(ref field, value);
                ValidateTimeRange();
            }
        } = null;

        public int StartHour
        {
            get;
            set => SetProperty(ref field, value);
        }
        public int StartMinute
        {
            get;
            set => SetProperty(ref field, value);
        }

        public int EndHour
        {
            get;
            set => SetProperty(ref field, value);
        }
        public int EndMinute
        {
            get;
            set => SetProperty(ref field, value);
        }

        // Dropdown tracking lists populate ranges automatically for view selection binding (00-23 hours, 00-59 minutes)
        public ObservableCollection<int> Hours { get; } = new ObservableCollection<int>(Enumerable.Range(0, 24));
        public ObservableCollection<int> Minutes { get; } = new ObservableCollection<int>(Enumerable.Range(0, 60));

        /// <summary>
        /// Gets or sets the list of active selectable severity visibility items on the dashboard view.
        /// </summary>
        public ObservableCollection<SeverityOptionViewModel> SeverityOptions { get; set; }

        /// <summary>
        /// Gets or sets the single active selected facility index filter constraint.
        /// </summary>
        public int? SelectedFacility
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the target UI view-grid should lock auto-scrolling to the latest incoming item.
        /// </summary>
        public bool AutoScroll
        {
            get;
            set => SetProperty(ref field, value);
        } = true;

        /// <summary>
        /// Gets the command responsible for restoring all filter properties back to default open states.
        /// </summary>
        public ICommand ClearFiltersCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterViewModel"/> class.
        /// </summary>
        public FilterViewModel()
        {
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            // Initialize the list collection with all priority configuration options checked by default
            SeverityOptions =
            [
                new SeverityOptionViewModel() { Name = "Emergency", Tag = 0, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Alert", Tag = 1, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Critical", Tag = 2, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Error", Tag = 3, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Warning", Tag = 4, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Notice", Tag = 5, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Info", Tag = 6, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Debug", Tag = 7, IsChecked = true }
            ];

            // Subscribe directly to checkbox modifications to instantly trigger an update on the message list
            foreach (var option in SeverityOptions)
            {
                option.PropertyChanged += (_, e) => {
                    if (e.PropertyName == nameof(SeverityOptionViewModel.IsChecked))
                    {
                        FiltersChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
            }

            // Fallback global event hook: if any parameter on this view-model shifts, signal listeners to re-evaluate items
            PropertyChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Notification hook raised whenever a UI state parameter shifts, informing consumer models to run a data refresh layout.
        /// </summary>
        public event EventHandler FiltersChanged;

        /// <summary>
        /// Resets all input filters back to baseline default structures.
        /// </summary>
        private void ClearFilters()
        {
            HostnameFilter = string.Empty;
            AppNameFilter = string.Empty;
            MessageFilter = string.Empty;
            SelectedSeverity = null;
            SelectedFacility = null;
            AutoScroll = true;
            StartTime = null;
            EndTime = null;

            foreach (var severityOptionViewModel in SeverityOptions)
            {
                severityOptionViewModel.IsChecked = true;
            }
        }

        /// <summary>
        /// Evaluates an incoming message packet against all structural rule sets.
        /// </summary>
        /// <param name="message">The message item payload to test.</param>
        /// <returns><c>true</c> if the log satisfies all filtering rules; otherwise, <c>false</c>.</returns>
        public bool MatchesFilters(SyslogMessage message)
        {
            // 1. Hostname processing (supports multi-line token arrays)
            if (!string.IsNullOrWhiteSpace(HostnameFilter))
            {
                var hostLines = HostnameFilter
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToList();

                if (hostLines.Count > 0)
                {
                    var matchesAnyHost = hostLines.Any(line =>
                        message.Hostname != null &&
                        message.Hostname.Contains(line, StringComparison.OrdinalIgnoreCase));

                    if (!matchesAnyHost) return false;
                }
            }

            // 2. Application tag processing
            if (!string.IsNullOrWhiteSpace(AppNameFilter))
            {
                var appNames = AppNameFilter
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToList();

                if (appNames.Count > 0)
                {
                    var matchesAnyHost = appNames.Any(line =>
                        message.AppName != null &&
                        message.AppName.Contains(line, StringComparison.OrdinalIgnoreCase));

                    if (!matchesAnyHost) return false;
                }
            }

            // 3. Message contextual body processing
            if (!string.IsNullOrWhiteSpace(MessageFilter))
            {
                var messages = MessageFilter
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToList();

                if (messages.Count > 0)
                {
                    var matchesAnyHost = messages.Any(line =>
                        message.Message != null &&
                        message.Message.Contains(line, StringComparison.OrdinalIgnoreCase));

                    if (!matchesAnyHost) return false;
                }
            }

            // 4. Severity multi-checkbox flag lookup evaluation
            var option = SeverityOptions.FirstOrDefault(o => o.Tag == (int)message.Severity);
            if (option != null && !option.IsChecked)
            {
                return false; // If the restriction option object is explicitly unchecked, hide the message
            }

            // 5. Facility classification verification
            if (SelectedFacility.HasValue && (int)message.Facility != SelectedFacility.Value)
                return false;

            // 6. Chronological baseline timestamp validation
            if (StartTime.HasValue)
            {
                var fullStart = GetCombinedDate(StartTime, StartHour, StartMinute);
                if (message.Timestamp < fullStart) return false;
            }

            // 7. Chronological ceiling timestamp validation
            if (EndTime.HasValue)
            {
                var fullEnd = GetCombinedDate(EndTime, EndHour, EndMinute);
                if (message.Timestamp > fullEnd) return false;
            }

            return true; // Pass through: item meets all criteria conditions successfully
        }

        /// <summary>
        /// Guards validation invariants preventing anomalies where an ending point precedes a start parameter.
        /// </summary>
        private void ValidateTimeRange()
        {
            if (StartTime.HasValue && EndTime.HasValue && EndTime.Value < StartTime.Value)
            {
                // If a user sets a concluding date prior to the initialization date, force clamp them to match
                EndTime = StartTime;
                OnPropertyChanged(nameof(EndTime));
            }
        }

        /// <summary>
        /// Compiles a combined structured timestamp struct by merging separate date pickers and numerical hour/minute dropdown selections.
        /// </summary>
        private DateTime GetCombinedDate(DateTime? date, int hour, int minute)
        {
            if (!date.HasValue) return DateTime.MinValue;
            return new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, hour, minute, 0);
        }
    }
}