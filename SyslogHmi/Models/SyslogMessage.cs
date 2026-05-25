using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SyslogHmi.Models
{
    /// <summary>
    /// Represents a parsed syslog message with metadata and UI-related visual properties.
    /// Implements INotifyPropertyChanged for the UI to react to visual property updates.
    /// </summary>
    public class SyslogMessage : INotifyPropertyChanged
    {
        /// <summary>
        /// Identifier for this message record (database or in-memory id).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Original timestamp parsed from the syslog message or assigned on receipt.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Hostname that sent the message.
        /// </summary>
        public string Hostname { get; set; } = string.Empty;

        /// <summary>
        /// Application or process name extracted from the message.
        /// </summary>
        public string AppName { get; set; } = string.Empty;

        /// <summary>
        /// Process identifier (when available).
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Message identifier (if present in protocol).
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Numeric severity level (0-7).
        /// </summary>
        public int Severity { get; set; } // 0-7

        /// <summary>
        /// Numeric facility code (0-23).
        /// </summary>
        public int Facility { get; set; } // 0-23

        /// <summary>
        /// Human-friendly severity name (e.g. "Error").
        /// </summary>
        public string SeverityName { get; set; } = string.Empty;

        /// <summary>
        /// Human-friendly facility name.
        /// </summary>
        public string FacilityName { get; set; } = string.Empty;

        /// <summary>
        /// Short message content.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Full original message text.
        /// </summary>
        public string FullMessage { get; set; } = string.Empty;

        /// <summary>
        /// Time the message was received by this application (UTC).
        /// </summary>
        public DateTime ReceivedTime { get; set; }

        // Visual properties that may change after the message is created and therefore need change notifications

        /// <summary>
        /// Background color applied to the message in the UI (friendly name or hex).
        /// </summary>
        public string BackgroundColor
        {
            get;
            set => SetProperty(ref field, value);
        } = "Transparent";

        /// <summary>
        /// Foreground/text color applied to the message in the UI.
        /// </summary>
        public string ForegroundColor
        {
            get;
            set => SetProperty(ref field, value);
        } = "Black";

        /// <summary>
        /// Font weight applied to the message in the UI.
        /// </summary>
        public FontWeight FontWeight
        {
            get;
            set => SetProperty(ref field, value);
        } = FontWeights.Normal;

        /// <summary>
        /// Font style applied to the message in the UI.
        /// </summary>
        public FontStyle FontStyle
        {
            get;
            set => SetProperty(ref field, value);
        } = FontStyles.Normal;

        /// <summary>
        /// Initializes a new instance of SyslogMessage and sets the ReceivedTime to UtcNow.
        /// </summary>
        public SyslogMessage()
        {
            ReceivedTime = DateTime.UtcNow;
        }

        // -------------------------------------------------------------------------
        // WPF NOTIFICATION: Standard implementation of INotifyPropertyChanged
        // -------------------------------------------------------------------------

        /// <summary>
        /// Occurs when a property value changes. Used by data binding in the UI.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the specified property name.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Helper to set a backing field and raise PropertyChanged when the value changes.
        /// Returns true when the value actually changed.
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
