using System;
using System.Linq;
using SyslogHmi.Models;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Provides utilities for validating and fixing color rules data integrity issues.
    /// </summary>
    public static class ColorRuleValidator
    {
        /// <summary>
        /// Validates and attempts to fix a color rule's conditions.
        /// Ensures PropertyName values are correctly set for Severity and Facility rules.
        /// </summary>
        /// <param name="rule">The color rule to validate.</param>
        /// <returns>True if the rule was valid or fixed; false if it could not be fixed.</returns>
        public static bool ValidateAndFixRule(ColorRule rule)
        {
            if (rule == null) return false;

            bool hasIssues = false;

            // Check each condition
            foreach (var condition in rule.Conditions)
            {
                // Check if PropertyName is empty or null
                if (string.IsNullOrEmpty(condition.PropertyName))
                {
                    System.Diagnostics.Debug.WriteLine($"[ColorRuleValidator] Empty PropertyName found in rule '{rule.Name}'");
                    hasIssues = true;
                    continue;
                }

                // Validate that PropertyName is a known property
                var validProperties = new[]
                {
                    nameof(SyslogMessage.Severity),
                    nameof(SyslogMessage.SeverityName),
                    nameof(SyslogMessage.Facility),
                    nameof(SyslogMessage.FacilityName),
                    nameof(SyslogMessage.Hostname),
                    nameof(SyslogMessage.AppName),
                    nameof(SyslogMessage.Message),
                    nameof(SyslogMessage.FullMessage),
                    nameof(SyslogMessage.ProcessId),
                    nameof(SyslogMessage.MessageId),
                    nameof(SyslogMessage.Timestamp)
                };

                if (!validProperties.Contains(condition.PropertyName))
                {
                    System.Diagnostics.Debug.WriteLine($"[ColorRuleValidator] Invalid PropertyName '{condition.PropertyName}' in rule '{rule.Name}'. Valid values are: {string.Join(", ", validProperties)}");
                    hasIssues = true;
                }

                // Check if ComparisonValue is empty when it shouldn't be
                if (string.IsNullOrEmpty(condition.ComparisonValue) && 
                    condition.ComparisonType != ComparisonType.RegexMatch &&
                    (condition.PropertyName == nameof(SyslogMessage.Severity) || 
                     condition.PropertyName == nameof(SyslogMessage.Facility)))
                {
                    System.Diagnostics.Debug.WriteLine($"[ColorRuleValidator] Empty ComparisonValue for {condition.PropertyName} condition in rule '{rule.Name}'");
                    hasIssues = true;
                }
            }

            return !hasIssues;
        }

        /// <summary>
        /// Scans all rules and reports which ones have potential issues.
        /// </summary>
        public static void ValidateAllRules(System.Collections.ObjectModel.ObservableCollection<ColorRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[ColorRuleValidator] No rules to validate");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ColorRuleValidator] Validating {rules.Count} rules...");

            int validCount = 0;
            int invalidCount = 0;

            foreach (var rule in rules)
            {
                if (ValidateAndFixRule(rule))
                {
                    validCount++;
                }
                else
                {
                    invalidCount++;
                    System.Diagnostics.Debug.WriteLine($"[ColorRuleValidator] Rule '{rule.Name}' (ID={rule.Id}) has issues");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ColorRuleValidator] Validation complete: {validCount} valid, {invalidCount} invalid");
        }
    }
}
