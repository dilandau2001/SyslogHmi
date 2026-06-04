namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// Represents a selectable severity toggle configuration option inside the filter control panel.
    /// Acts as a data-bindable bridge between the UI checkbox elements and the core filtering logic.
    /// </summary>
    public class SeverityOptionViewModel : ViewModelBase
    {
        /// <summary>
        /// Gets or sets the human-readable display string for the severity level (e.g., "Critical", "Warning", "Info").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the standard numeric identifier corresponding to the Syslog severity index (Range: 0 to 7).
        /// </summary>
        public int Tag { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this specific severity category filter is actively checked by the operator.
        /// Raises a property change notification instantly upon value modification.
        /// </summary>
        public bool IsChecked
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(); // Fires notification using the naming context of the property
                }
            }
        }
    }
}