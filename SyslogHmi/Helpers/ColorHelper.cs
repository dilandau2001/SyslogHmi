using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SyslogHmi.Models;

namespace SyslogHmi.Helpers
{
    /// <summary>
    /// Utility helpers to manage friendly color names, lookup hex values and build ColorItem collections for the UI.
    /// </summary>
    public static class ColorHelper
    {
        /// <summary>
        /// Friendly color name -> hex value map used by the UI.
        /// </summary>
        public static readonly Dictionary<string, string> FriendlyColors = new()
        {
            { "White", "#FFFFFF" },
            { "Black", "#000000" },
            { "Red", "#FF0000" },
            { "Green", "#00AA00" },
            { "Blue", "#0000FF" },
            { "Yellow", "#FFFF00" },
            { "Cyan", "#00FFFF" },
            { "Magenta", "#FF00FF" },
            { "Gray", "#808080" },
            { "Dark Red", "#AA0000" },
            { "Dark Green", "#00AA00" },
            { "Dark Blue", "#0000AA" },
            { "Light Red", "#FF6666" },
            { "Light Green", "#66FF66" },
            { "Light Blue", "#6666FF" },
            { "Orange", "#FFA500" },
            { "Purple", "#800080" },
            { "Brown", "#A52A2A" }
        };

        /// <summary>
        /// Returns the friendly color names for binding to a combo box.
        /// </summary>
        public static List<string> GetFriendlyColorNames()
        {
            return FriendlyColors.Keys.ToList();
        }

        /// <summary>
        /// Constructs an ObservableCollection of ColorItem for UI previews.
        /// </summary>
        public static ObservableCollection<ColorItem> GetColorItems()
        {
            var items = new ObservableCollection<ColorItem>();
            foreach (var kvp in FriendlyColors)
            {
                items.Add(new ColorItem(kvp.Key, kvp.Value));
            }
            return items;
        }

        /// <summary>
        /// Maps a friendly name to its hex representation. Returns white as a fallback.
        /// </summary>
        public static string GetHexFromFriendlyName(string friendlyName)
        {
            return FriendlyColors.TryGetValue(friendlyName, out var hex) ? hex : "#FFFFFF";
        }

        /// <summary>
        /// Finds the friendly name for a hex value. If no friendly name exists, returns the hex value itself.
        /// </summary>
        public static string GetFriendlyNameFromHex(string hex)
        {
            var entry = FriendlyColors.FirstOrDefault(kvp => kvp.Value.Equals(hex, System.StringComparison.OrdinalIgnoreCase));
            return entry.Key ?? hex;
        }

        // Severity colors
        public static readonly Dictionary<int, string> SeverityFriendlyNames = new()
        {
            { 0, "Emergency" },
            { 1, "Alert" },
            { 2, "Critical" },
            { 3, "Error" },
            { 4, "Warning" },
            { 5, "Notice" },
            { 6, "Info" },
            { 7, "Debug" }
        };

        /// <summary>
        /// Returns the friendly name for a numeric severity level.
        /// </summary>
        public static string GetSeverityName(int severityLevel)
        {
            return SeverityFriendlyNames.TryGetValue(severityLevel, out var name) ? name : "Unknown";
        }

        /// <summary>
        /// Returns the numeric severity level for a friendly name. If not found, returns the default map key (0).
        /// </summary>
        public static int GetSeverityLevel(string severityName)
        {
            var entry = SeverityFriendlyNames.FirstOrDefault(kvp => kvp.Value.Equals(severityName, System.StringComparison.OrdinalIgnoreCase));
            return entry.Key;
        }
    }
}
