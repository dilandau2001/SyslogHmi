using SyslogHmi.Models;
using SyslogHmi.Extensions;
using System;
using System.Globalization;

namespace SyslogHmi.Services
{
    public sealed class Rfc3164Parser : ISyslogParser
    {
        public SyslogMessage Parse(string rawMessage)
        {
            if (string.IsNullOrWhiteSpace(rawMessage))
                return null;

            if (rawMessage[0] != '<')
                return null;

            var textSpan = rawMessage.AsSpan().Trim();

            if (textSpan.Length < 4)
                return null;

            var priEnd = textSpan.IndexOf('>');
            if (priEnd == -1)
                return null;

            var priSpan = textSpan.Slice(1, priEnd - 1);
            if (!int.TryParse(priSpan, out var pri))
                return null;

            var isValidPri = pri >= 0 && pri <= 191;

            var facilityValue = isValidPri ? (pri >> 3) & 0x1F : -1;
            var severityValue = isValidPri ? pri & 0x07 : -1;

            var message = new SyslogMessage
            {
                FullMessage = rawMessage,
                ReceivedTime = DateTime.UtcNow,
                Facility = facilityValue.ToSyslogFacility(),
                Severity = severityValue.ToSyslogSeverity()
            };

            var span = textSpan.Slice(priEnd + 1);

            // CHANGED: Changed from 16 to 15. The standard BSD timestamp is exactly 15 characters.
            if (span.Length < 15)
            {
                message.Timestamp = DateTime.UtcNow;
                message.Message = span.ToString();
                return message;
            }

            var timeSpan = span.Slice(0, 15);
            string[] formats = ["MMM dd HH:mm:ss", "MMM  d HH:mm:ss", "MMM d HH:mm:ss"];

            if (DateTime.TryParseExact(timeSpan, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out var parsedDate))
            {
                message.Timestamp = new DateTime(DateTime.UtcNow.Year, parsedDate.Month, parsedDate.Day,
                                                 parsedDate.Hour, parsedDate.Minute, parsedDate.Second, DateTimeKind.Utc);
            }
            else
            {
                message.Timestamp = DateTime.UtcNow;
            }

            // Guard against index out of bounds if the string ends exactly after the timestamp
            if (span.Length <= 16)
            {
                return message;
            }

            span = span.Slice(16);

            var spaceIdx = span.IndexOf(' ');
            if (spaceIdx == -1) return message;
            message.Hostname = span.Slice(0, spaceIdx).ToString();
            span = span.Slice(spaceIdx + 1);

            var colonIdx = span.IndexOf(':');
            if (colonIdx != -1)
            {
                var tagSpan = span.Slice(0, colonIdx);
                var bracketOpen = tagSpan.IndexOf('[');
                var bracketClose = tagSpan.IndexOf(']');

                if (bracketOpen != -1 && bracketClose != -1 && bracketClose > bracketOpen)
                {
                    message.AppName = tagSpan.Slice(0, bracketOpen).ToString();
                    var pidSpan = tagSpan.Slice(bracketOpen + 1, bracketClose - bracketOpen - 1);
                    if (int.TryParse(pidSpan, out var pid))
                    {
                        message.ProcessId = pid;
                    }
                }
                else
                {
                    message.AppName = tagSpan.ToString();
                }

                if (colonIdx + 1 < span.Length && span[colonIdx + 1] == ' ')
                    message.Message = span.Slice(colonIdx + 2).ToString();
                else
                    message.Message = span.Slice(colonIdx + 1).ToString();
            }
            else
            {
                message.Message = span.ToString();
            }

            return message;
        }
    }
}