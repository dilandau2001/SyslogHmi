using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SyslogHmi.Models
{
    /// <summary>
    /// Represents a single condition used by a color rule.
    /// The condition targets a property of a SyslogMessage and evaluates it using the selected ComparisonType.
    /// </summary>
    public class ColorCondition
    {
        /// <summary>
        /// The name of the syslog message property to evaluate (e.g. "Severity", "Hostname").
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// The comparison operation to perform when evaluating the property.
        /// </summary>
        public ComparisonType ComparisonType { get; set; }

        /// <summary>
        /// The primary comparison value used by the ComparisonType.
        /// </summary>
        public string ComparisonValue { get; set; } = string.Empty;

        /// <summary>
        /// Whether the comparison is case-sensitive.
        /// </summary>
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Alternative values used with OR logic. If set, the condition matches when either ComparisonValue or any AlternativeValues match.
        /// </summary>
        public List<string> AlternativeValues { get; set; } = new();

        /// <summary>
        /// Evaluates this condition against the provided SyslogMessage instance.
        /// </summary>
        /// <param name="message">The message to evaluate.</param>
        /// <returns>True if the condition passes; otherwise false.</returns>
        public bool Evaluate(SyslogMessage message)
        {
            var propertyValue = GetPropertyValue(message);
            if (propertyValue == null) return false;

            var stringComparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // Evaluate the primary comparison value
            var matchesPrimary = EvaluateValue(propertyValue, ComparisonValue, stringComparison);

            // If there are alternative values, apply OR logic against them as well
            if (AlternativeValues.Count > 0)
            {
                return matchesPrimary || AlternativeValues.Any(alt => EvaluateValue(propertyValue, alt, stringComparison));
            }

            return matchesPrimary;
        }

        /// <summary>
        /// Evaluates a single comparison between the property value and a comparison value using the specified StringComparison.
        /// </summary>
        private bool EvaluateValue(string propertyValue, string comparisonValue, StringComparison stringComparison)
        {
            return ComparisonType switch
            {
                ComparisonType.Equals => 
                    propertyValue.Equals(comparisonValue, stringComparison),

                ComparisonType.NotEquals => 
                    !propertyValue.Equals(comparisonValue, stringComparison),

                ComparisonType.Contains => 
                    propertyValue.Contains(comparisonValue, stringComparison),

                ComparisonType.NotContains => 
                    !propertyValue.Contains(comparisonValue, stringComparison),

                ComparisonType.StartsWith => 
                    propertyValue.StartsWith(comparisonValue, stringComparison),

                ComparisonType.EndsWith => 
                    propertyValue.EndsWith(comparisonValue, stringComparison),

                ComparisonType.GreaterThan => 
                    int.TryParse(propertyValue, out var val1) && 
                    int.TryParse(comparisonValue, out var val2) && val1 > val2,

                ComparisonType.LessThan => 
                    int.TryParse(propertyValue, out var val3) && 
                    int.TryParse(comparisonValue, out var val4) && val3 < val4,

                ComparisonType.RegexMatch => 
                    TryRegexMatch(propertyValue, comparisonValue),

                _ => false
            };
        }

        /// <summary>
        /// Retrieves the string representation of the targeted property from the SyslogMessage.
        /// Returns null when the PropertyName is not recognized.
        /// </summary>
        private string GetPropertyValue(SyslogMessage message)
        {
            return PropertyName switch
            {
                nameof(SyslogMessage.Severity) => message.Severity.ToString(),
                nameof(SyslogMessage.SeverityName) => message.SeverityName,
                nameof(SyslogMessage.Facility) => message.Facility.ToString(),
                nameof(SyslogMessage.FacilityName) => message.FacilityName,
                nameof(SyslogMessage.Hostname) => message.Hostname,
                nameof(SyslogMessage.AppName) => message.AppName,
                nameof(SyslogMessage.Message) => message.Message,
                nameof(SyslogMessage.FullMessage) => message.FullMessage,
                nameof(SyslogMessage.ProcessId) => message.ProcessId.ToString(),
                nameof(SyslogMessage.MessageId) => message.MessageId,
                nameof(SyslogMessage.Timestamp) => message.Timestamp.ToString(),
                _ => null
            };
        }

        /// <summary>
        /// Tries to perform a regular expression match between the input and the provided pattern.
        /// Returns false if the regex pattern is invalid.
        /// </summary>
        private bool TryRegexMatch(string input, string pattern)
        {
            try
            {
                return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Comparison operations supported by ColorCondition.
    /// </summary>
    public enum ComparisonType
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        RegexMatch
    }
}
