using SyslogHmi.Models;
using System;

namespace SyslogHmi.Extensions
{
    /// <summary>
    /// Provides static conversion methods to safely map integer metrics into official Syslog Enums.
    /// </summary>
    public static class SyslogEnumExtensions
    {
        /// <summary>
        /// Converts an integer facility value to its corresponding SyslogFacility enum representation.
        /// </summary>
        /// <param name="facilityValue">The raw calculated numerical integer value of the facility chunk.</param>
        /// <returns>A valid SyslogFacility enum token, or SyslogFacility.Unknown if out of legal bounds.</returns>
        public static SyslogFacility ToSyslogFacility(this int facilityValue)
        {
            return Enum.IsDefined(typeof(SyslogFacility), facilityValue)
                ? (SyslogFacility)facilityValue
                : SyslogFacility.Unknown;
        }

        /// <summary>
        /// Converts an integer severity value to its corresponding SyslogSeverity enum representation.
        /// </summary>
        /// <param name="severityValue">The raw isolated numerical integer value of the severity chunk.</param>
        /// <returns>A valid SyslogSeverity enum token, or SyslogSeverity.Unknown if out of legal bounds.</returns>
        public static SyslogSeverity ToSyslogSeverity(this int severityValue)
        {
            return Enum.IsDefined(typeof(SyslogSeverity), severityValue)
                ? (SyslogSeverity)severityValue
                : SyslogSeverity.Unknown;
        }
    }
}