using System.Windows.Media;

namespace SyslogHmi.Models
{
    /// <summary>
    /// Represents a named color option with its hex value and a SolidColorBrush for UI binding.
    /// </summary>
    public class ColorItem
    {
        /// <summary>
        /// Friendly name for the color (e.g. "White", "Red").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Hex value for the color (e.g. "#FFFFFF").
        /// </summary>
        public string HexValue { get; set; }

        /// <summary>
        /// A SolidColorBrush created from the HexValue for data binding in the UI.
        /// </summary>
        public SolidColorBrush ColorBrush { get; set; }

        /// <summary>
        /// Initializes a new ColorItem from a friendly name and a hex string. If the hex string cannot be converted,
        /// a white brush is assigned as a safe fallback.
        /// </summary>
        public ColorItem(string name, string hexValue)
        {
            Name = name;
            HexValue = hexValue;
            try
            {
                ColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexValue));
            }
            catch
            {
                ColorBrush = new SolidColorBrush(Colors.White);
            }
        }

        /// <summary>
        /// Returns the friendly name for display/debugging purposes.
        /// </summary>
        public override string ToString() => Name;
    }
}
