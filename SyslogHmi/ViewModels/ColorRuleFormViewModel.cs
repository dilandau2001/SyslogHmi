using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SyslogHmi.Helpers;
using SyslogHmi.Models;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// ViewModel used by the color rule editor form. It holds temporary state while editing or creating a ColorRule.
    /// </summary>
    public class ColorRuleFormViewModel : ViewModelBase
    {
        private ColorRule _editingRule;

        /// <summary>
        /// Friendly color names usable in the combo boxes.
        /// </summary>
        public ObservableCollection<string> AvailableColors { get; }

        /// <summary>
        /// Color items including brushes for previews.
        /// </summary>
        public ObservableCollection<ColorItem> AvailableColorItems { get; }

        /// <summary>
        /// Severity check options (0..7).
        /// </summary>
        public ObservableCollection<SeverityCheckItem> SeverityOptions { get; }

        /// <summary>
        /// Facility check options.
        /// </summary>
        public ObservableCollection<FacilityCheckItem> FacilityOptions { get; }

        /// <summary>
        /// Rule display name bound to the form.
        /// </summary>
        public string RuleName
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        /// <summary>
        /// Selected background color name from the UI.
        /// </summary>
        public string SelectedBackgroundColor
        {
            get;
            set => SetProperty(ref field, value);
        } = "White";

        /// <summary>
        /// Selected foreground color name from the UI.
        /// </summary>
        public string SelectedForegroundColor
        {
            get;
            set => SetProperty(ref field, value);
        } = "Black";

        /// <summary>
        /// Whether the form is editing an existing rule (true) or creating a new one (false).
        /// </summary>
        public bool IsEditMode
        {
            get;
            set => SetProperty(ref field, value);
        }

        // Severity properties
        public bool SeverityFilterEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        // Hostname properties
        public bool HostnameFilterEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string HostnameFilterText
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public bool HostnameCaseSensitive
        {
            get;
            set => SetProperty(ref field, value);
        }

        // App Name properties
        public bool AppNameFilterEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string AppNameFilterText
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public bool AppNameCaseSensitive
        {
            get;
            set => SetProperty(ref field, value);
        }

        // Facility properties
        public bool FacilityFilterEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string SelectedFacility
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        // Message properties
        public bool MessageFilterEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string MessageFilterText
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public bool MessageCaseSensitive
        {
            get;
            set => SetProperty(ref field, value);
        }

        // ProcessId properties
        public bool PidFilterEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string PidFilterText
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        // MessageId properties
        public bool MsgIdFilterEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string MsgIdFilterText
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public ICommand SaveRuleCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }

        public event EventHandler<ColorRule> RuleSaved;
        public event EventHandler Cancelled;

        public ColorRuleFormViewModel()
        {
            AvailableColors = new ObservableCollection<string>(ColorHelper.GetFriendlyColorNames());
            AvailableColorItems = ColorHelper.GetColorItems();

            SeverityOptions = [];
            for (var i = 0; i <= 7; i++)
            {
                SeverityOptions.Add(new SeverityCheckItem
                {
                    Level = i,
                    Name = ColorHelper.GetSeverityName(i),
                    IsChecked = false
                });
            }

            FacilityOptions = [];
            var facilities = new[] { "Kernel", "User", "Mail", "Daemon", "Auth", "Syslog", "LPR", "News", "Local0", "Local1", "Local2", "Local3", "Local4", "Local5", "Local6", "Local7" };
            for (var i = 0; i < facilities.Length; i++)
            {
                FacilityOptions.Add(new FacilityCheckItem
                {
                    Level = i,
                    Name = facilities[i],
                    IsChecked = false
                });
            }

            SaveRuleCommand = new RelayCommand(_ => SaveRule(), _ => CanSaveRule());
            CancelCommand = new RelayCommand(_ => OnCancel());
            ResetCommand = new RelayCommand(_ => Reset());
        }

        /// <summary>
        /// Loads an existing ColorRule into the form view model for editing.
        /// The method maps conditions and formatting into the form fields.
        /// </summary>
        /// <param name="rule">The ColorRule to edit.</param>
        public void LoadRuleForEditing(ColorRule rule)
        {
            _editingRule = rule;
            IsEditMode = true;
            RuleName = rule.Name;
            SelectedBackgroundColor = ColorHelper.GetFriendlyNameFromHex(rule.Format.BackgroundColor);
            SelectedForegroundColor = ColorHelper.GetFriendlyNameFromHex(rule.Format.ForegroundColor);

            // Clear all filters first
            ClearAllFilters();

            // Load conditions
            foreach (var condition in rule.Conditions)
            {
                if (condition.PropertyName == nameof(SyslogMessage.Severity))
                {
                    SeverityFilterEnabled = true;
                    if (int.TryParse(condition.ComparisonValue, out var level))
                    {
                        if (level >= 0 && level <= 7)
                            SeverityOptions[level].IsChecked = true;
                    }
                    foreach (var alt in condition.AlternativeValues)
                    {
                        if (int.TryParse(alt, out var altLevel) && altLevel >= 0 && altLevel <= 7)
                            SeverityOptions[altLevel].IsChecked = true;
                    }
                }
                else if (condition.PropertyName == nameof(SyslogMessage.Hostname))
                {
                    HostnameFilterEnabled = true;
                    HostnameFilterText = condition.ComparisonValue;
                    HostnameCaseSensitive = condition.CaseSensitive;
                }
                else if (condition.PropertyName == nameof(SyslogMessage.AppName))
                {
                    AppNameFilterEnabled = true;
                    AppNameFilterText = condition.ComparisonValue;
                    AppNameCaseSensitive = condition.CaseSensitive;
                }
                else if (condition.PropertyName == nameof(SyslogMessage.Facility))
                {
                    FacilityFilterEnabled = true;
                    if (int.TryParse(condition.ComparisonValue, out var fac))
                    {
                        if (fac >= 0 && fac < FacilityOptions.Count)
                            FacilityOptions[fac].IsChecked = true;
                    }
                }
                else if (condition.PropertyName == nameof(SyslogMessage.Message))
                {
                    MessageFilterEnabled = true;
                    MessageFilterText = condition.ComparisonValue;
                    MessageCaseSensitive = condition.CaseSensitive;
                }
                else if (condition.PropertyName == nameof(SyslogMessage.ProcessId))
                {
                    PidFilterEnabled = true;
                    PidFilterText = condition.ComparisonValue;
                }
                else if (condition.PropertyName == nameof(SyslogMessage.MessageId))
                {
                    MsgIdFilterEnabled = true;
                    MsgIdFilterText = condition.ComparisonValue;
                }
            }
        }

        /// <summary>
        /// Builds a ColorRule from the form fields and raises the RuleSaved event.
        /// Handles both create and update flows depending on IsEditMode.
        /// </summary>
        private void SaveRule()
        {
            if (!CanSaveRule()) return;

            ColorRule rule;
            if (IsEditMode && _editingRule != null)
            {
                rule = new ColorRule
                {
                    Id = _editingRule.Id,          // Important so the database treats this as an UPDATE
                    Priority = _editingRule.Priority, // Preserve current ordering
                    IsActive = _editingRule.IsActive,
                    Name = RuleName,
                    Format = new ColorFormat(
                        ColorHelper.GetHexFromFriendlyName(SelectedBackgroundColor),
                        ColorHelper.GetHexFromFriendlyName(SelectedForegroundColor),
                        false,
                        false
                    )
                };
            }
            else
            {
                // Normal flow for creating a new rule
                rule = new ColorRule
                {
                    Name = RuleName,
                    Format = new ColorFormat(
                        ColorHelper.GetHexFromFriendlyName(SelectedBackgroundColor),
                        ColorHelper.GetHexFromFriendlyName(SelectedForegroundColor),
                        false,
                        false
                    ),
                    IsActive = true,
                    Priority = 0
                };
            }

            // Add severity conditions
            if (SeverityFilterEnabled)
            {
                var checkedSeverities = SeverityOptions.Where(s => s.IsChecked).ToList();
                if (checkedSeverities.Count > 0)
                {
                    var severityCondition = new ColorCondition
                    {
                        PropertyName = nameof(SyslogMessage.Severity),
                        ComparisonType = ComparisonType.Equals,
                        ComparisonValue = checkedSeverities.First().Level.ToString(),
                        CaseSensitive = false
                    };

                    foreach (var severity in checkedSeverities.Skip(1))
                    {
                        severityCondition.AlternativeValues.Add(severity.Level.ToString());
                    }

                    rule.Conditions.Add(severityCondition);
                }
            }

            // Add hostname filter
            if (HostnameFilterEnabled && !string.IsNullOrWhiteSpace(HostnameFilterText))
            {
                rule.Conditions.Add(new ColorCondition
                {
                    PropertyName = nameof(SyslogMessage.Hostname),
                    ComparisonType = ComparisonType.Contains,
                    ComparisonValue = HostnameFilterText,
                    CaseSensitive = HostnameCaseSensitive
                });
            }

            // Add app name filter
            if (AppNameFilterEnabled && !string.IsNullOrWhiteSpace(AppNameFilterText))
            {
                rule.Conditions.Add(new ColorCondition
                {
                    PropertyName = nameof(SyslogMessage.AppName),
                    ComparisonType = ComparisonType.Contains,
                    ComparisonValue = AppNameFilterText,
                    CaseSensitive = AppNameCaseSensitive
                });
            }

            // Add facility filter
            if (FacilityFilterEnabled)
            {
                var checkedFacilities = FacilityOptions.Where(f => f.IsChecked).ToList();
                if (checkedFacilities.Count > 0)
                {
                    var facilityCondition = new ColorCondition
                    {
                        PropertyName = nameof(SyslogMessage.Facility),
                        ComparisonType = ComparisonType.Equals,
                        ComparisonValue = checkedFacilities.First().Level.ToString(),
                        CaseSensitive = false
                    };

                    foreach (var facility in checkedFacilities.Skip(1))
                    {
                        facilityCondition.AlternativeValues.Add(facility.Level.ToString());
                    }

                    rule.Conditions.Add(facilityCondition);
                }
            }

            // Add message filter
            if (MessageFilterEnabled && !string.IsNullOrWhiteSpace(MessageFilterText))
            {
                rule.Conditions.Add(new ColorCondition
                {
                    PropertyName = nameof(SyslogMessage.Message),
                    ComparisonType = ComparisonType.Contains,
                    ComparisonValue = MessageFilterText,
                    CaseSensitive = MessageCaseSensitive
                });
            }

            // Add ProcessId filter
            if (PidFilterEnabled && !string.IsNullOrWhiteSpace(PidFilterText))
            {
                rule.Conditions.Add(new ColorCondition
                {
                    PropertyName = nameof(SyslogMessage.ProcessId),
                    ComparisonType = ComparisonType.Equals,
                    ComparisonValue = PidFilterText,
                    CaseSensitive = false
                });
            }

            // Add MessageId filter
            if (MsgIdFilterEnabled && !string.IsNullOrWhiteSpace(MsgIdFilterText))
            {
                rule.Conditions.Add(new ColorCondition
                {
                    PropertyName = nameof(SyslogMessage.MessageId),
                    ComparisonType = ComparisonType.Contains,
                    ComparisonValue = MsgIdFilterText,
                    CaseSensitive = false
                });
            }

            RuleSaved?.Invoke(this, rule);
            Reset();
        }

        /// <summary>
        /// Determines whether the current form state is valid to save a rule.
        /// </summary>
        private bool CanSaveRule()
        {
            return !string.IsNullOrWhiteSpace(RuleName) &&
                   (SeverityFilterEnabled || HostnameFilterEnabled || AppNameFilterEnabled ||
                    FacilityFilterEnabled || MessageFilterEnabled || PidFilterEnabled || MsgIdFilterEnabled);
        }

        /// <summary>
        /// Cancels the edit and notifies listeners.
        /// </summary>
        private void OnCancel()
        {
            Reset();
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clears all filter selections in the form.
        /// </summary>
        private void ClearAllFilters()
        {
            SeverityFilterEnabled = false;
            HostnameFilterEnabled = false;
            AppNameFilterEnabled = false;
            FacilityFilterEnabled = false;
            MessageFilterEnabled = false;
            PidFilterEnabled = false;
            MsgIdFilterEnabled = false;

            foreach (var severity in SeverityOptions)
                severity.IsChecked = false;
            foreach (var facility in FacilityOptions)
                facility.IsChecked = false;
        }

        /// <summary>
        /// Resets the form to its default state for a new rule entry.
        /// </summary>
        public void Reset()
        {
            _editingRule = null;
            IsEditMode = false;
            RuleName = string.Empty;
            SelectedBackgroundColor = "White";
            SelectedForegroundColor = "Black";
            HostnameFilterText = string.Empty;
            AppNameFilterText = string.Empty;
            MessageFilterText = string.Empty;
            PidFilterText = string.Empty;
            MsgIdFilterText = string.Empty;

            ClearAllFilters();
        }
    }

    public class SeverityCheckItem : ViewModelBase
    {
        public int Level
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string Name
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public bool IsChecked
        {
            get;
            set => SetProperty(ref field, value);
        }
    }

    
}
