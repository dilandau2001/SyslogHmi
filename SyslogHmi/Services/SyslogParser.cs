using SyslogHmi.Models;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Router engine executing predictive analysis on incoming syslog headers to 
    /// dynamically delegate work to the correct polymorphic RFC strategy implementation.
    /// </summary>
    public sealed class SyslogParser : ISyslogParser
    {
        private readonly ISyslogParser _rfc3164Parser = new Rfc3164Parser();
        private readonly ISyslogParser _rfc5424Parser = new Rfc5424Parser();

        public SyslogMessage Parse(string rawMessage)
        {
            if (string.IsNullOrWhiteSpace(rawMessage))
                return null;

            // Simple preview look-ahead to find out which strategy to execute
            var closingBracketIdx = rawMessage.IndexOf('>');
            if (closingBracketIdx == -1 || closingBracketIdx + 1 >= rawMessage.Length)
                return _rfc3164Parser.Parse(rawMessage); // Let fallback parsing catch malformed inputs

            var lookAheadChar = rawMessage[closingBracketIdx + 1];

            // If a digit immediately follows the closing bracket, it's an IETF RFC 5424 message
            if (char.IsDigit(lookAheadChar))
            {
                return _rfc5424Parser.Parse(rawMessage);
            }

            return _rfc3164Parser.Parse(rawMessage);
        }
    }
}