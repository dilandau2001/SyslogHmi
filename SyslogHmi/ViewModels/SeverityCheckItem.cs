namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// Represents an individual item in a checkable selection list within the user interface.
    /// Used in the dashboard filter configuration panels to dynamically filter incoming Syslog streams by their Severity level.
    /// </summary>
    public class SeverityCheckItem : ViewModelBase
    {
        /// <summary>
        /// Gets or sets the numerical value corresponding to the standard Syslog severity level (Range: 0 for Emergency down to 7 for Debug).
        /// </summary>
        public int Level
        {
            get;
            set => SetProperty(ref field, value);
        }

        /// <summary>
        /// Gets or sets the human-readable display name of the severity category (e.g., "Emergency", "Error", "Debug").
        /// </summary>
        public string Name
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether logs belonging to this specific severity classification should be visible on the display layout.
        /// Changes to this property immediately trigger UI data binding updates.
        /// </summary>
        public bool IsChecked
        {
            get;
            set => SetProperty(ref field, value);
        }
    }
}