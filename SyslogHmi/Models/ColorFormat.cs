using System.Windows;

namespace SyslogHmi.Models
{
    /// <summary>
    /// Represents the visual formatting (background, foreground, and font styles) applied by a color rule.
    /// </summary>
    public class ColorFormat
    {
        /// <summary>
        /// Hex string for the background color (e.g. "#FFFFFF").
        /// </summary>
        public string BackgroundColor { get; set; } = "#FFFFFF";

        /// <summary>
        /// Hex string for the foreground/text color.
        /// </summary>
        public string ForegroundColor { get; set; } = "#000000";

        /// <summary>
        /// Whether the text should be rendered bold.
        /// </summary>
        public bool IsBold { get; set; }

        /// <summary>
        /// Whether the text should be rendered italic.
        /// </summary>
        public bool IsItalic { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ColorFormat()
        {
        }

        /// <summary>
        /// Creates a ColorFormat with the specified colors and font style flags.
        /// </summary>
        public ColorFormat(string backgroundColor, string foregroundColor, bool isBold = false, bool isItalic = false)
        {
            BackgroundColor = backgroundColor;
            ForegroundColor = foregroundColor;
            IsBold = isBold;
            IsItalic = isItalic;
        }

        /// <summary>
        /// Returns the appropriate FontStyle based on the IsItalic flag.
        /// Note: FontStyles provides named instances whose type is FontStyle.
        /// </summary>
        public FontStyle GetFontStyle()
        {
            return IsItalic ? FontStyles.Italic : FontStyles.Normal;
        }

        /// <summary>
        /// Returns the appropriate FontWeight based on the IsBold flag.
        /// </summary>
        public FontWeight GetFontWeight()
        {
            return IsBold ? FontWeights.Bold : FontWeights.Normal;
        }

        /// <summary>
        /// Creates a deep copy of this ColorFormat.
        /// </summary>
        public ColorFormat Clone()
        {
            return new ColorFormat(BackgroundColor, ForegroundColor, IsBold, IsItalic);
        }
    }
}
