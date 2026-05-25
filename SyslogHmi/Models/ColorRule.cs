using System.Collections.ObjectModel;
using System.Linq;

namespace SyslogHmi.Models
{
    /// <summary>
    /// Represents a color rule composed of multiple conditions and a resulting visual format.
    /// </summary>
    public class ColorRule
    {
        /// <summary>
        /// Database identifier for the rule.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Friendly name for the rule.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The set of conditions that must all evaluate to true for this rule to apply.
        /// </summary>
        public ObservableCollection<ColorCondition> Conditions { get; set; } = new ObservableCollection<ColorCondition>();

        /// <summary>
        /// Formatting to apply when the rule matches (colors, font style).
        /// </summary>
        public ColorFormat Format { get; set; } = new();

        /// <summary>
        /// Whether this rule is currently active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority determines ordering when multiple rules match.
        /// Lower numbers typically have higher priority depending on usage.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Determines whether this rule matches the provided SyslogMessage.
        /// All conditions must match (logical AND) and the rule must be active.
        /// </summary>
        public bool Matches(SyslogMessage message)
        {
            if (!IsActive) return false;
            if (Conditions.Count == 0) return false;

            // All conditions must match (AND logic)
            return Conditions.All(c => c.Evaluate(message));
        }
    }
}
