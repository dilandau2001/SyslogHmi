using SyslogHmi.Models;

namespace SyslogHmi.Services
{
    public interface ISyslogParser
    {
        /// <summary>
        /// Parses a raw network text string into a structured SyslogMessage model representation.
        /// </summary>
        /// <param name="rawMessage">The unmodified log line received from the network stream.</param>
        /// <returns>A structured SyslogMessage model instance, or null if the validation fails.</returns>
        SyslogMessage Parse(string rawMessage);
    }
}
