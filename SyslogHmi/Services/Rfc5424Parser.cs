using SyslogHmi.Extensions;
using SyslogHmi.Models;
using System;
using System.Globalization;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Tolerant RFC5424 syslog parser optimized for real-world log ingestion.
    /// 
    /// Design goals:
    /// - Never throw on malformed input
    /// - Extract as much information as possible
    /// - Support truncated or partially malformed messages
    /// - Minimize allocations using ReadOnlySpan<char>
    /// 
    /// This parser is intentionally permissive and does not perform
    /// strict RFC5424 semantic validation.
    /// </summary>
    public sealed class Rfc5424Parser : ISyslogParser
    {
        public SyslogMessage Parse(string rawMessage)
        {
            // Reject null, empty, or whitespace-only payloads.
            if (string.IsNullOrWhiteSpace(rawMessage))
                return null;

            // RFC5424 messages must begin with PRI opening bracket.
            if (rawMessage[0] != '<')
                return null;

            ReadOnlySpan<char> textSpan = rawMessage.AsSpan().Trim();

            // Smallest realistic RFC5424 message is longer than this.
            if (textSpan.Length < 7)
                return null;

            // Locate PRI closing bracket.
            int priEnd = textSpan.IndexOf('>');
            if (priEnd == -1)
                return null;

            // Parse PRI value.
            var priSpan = textSpan.Slice(1, priEnd - 1);

            if (!int.TryParse(priSpan, out int pri))
                return null;

            // PRI validation is tolerant:
            // invalid PRI values do not reject the message,
            // but Facility/Severity become Unknown/default.
            bool isValidPri = pri >= 0 && pri <= 191;

            int facilityValue = isValidPri
                ? (pri >> 3) & 0x1F
                : -1;

            int severityValue = isValidPri
                ? pri & 0x07
                : -1;

            var message = new SyslogMessage
            {
                FullMessage = rawMessage,
                ReceivedTime = DateTime.UtcNow,
                Facility = facilityValue.ToSyslogFacility(),
                Severity = severityValue.ToSyslogSeverity()
            };

            // Advance past PRI.
            ReadOnlySpan<char> span = textSpan.Slice(priEnd + 1);

            // -----------------------------------------------------------------
            // VERSION
            // -----------------------------------------------------------------
            // RFC5424 version field is intentionally ignored.
            // Parser operates in best-effort mode rather than strict validation.

            int versionSpace = span.IndexOf(' ');

            // Message truncated immediately after VERSION.
            if (versionSpace == -1)
                return message;

            span = span.Slice(versionSpace + 1);

            // -----------------------------------------------------------------
            // 1. TIMESTAMP
            // -----------------------------------------------------------------

            int spaceIdx = span.IndexOf(' ');

            // Message truncated during TIMESTAMP.
            if (spaceIdx == -1)
                return message;

            var timestampSpan = span.Slice(0, spaceIdx);

            // NILVALUE ("-") means timestamp is unavailable.
            if (timestampSpan.Length > 1 && timestampSpan[0] != '-')
            {
                // If timestamp parsing fails, fallback to current UTC time
                // instead of rejecting the entire message.
                if (DateTime.TryParse(
                    timestampSpan.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime parsedDate))
                {
                    message.Timestamp = parsedDate.ToUniversalTime();
                }
                else
                {
                    message.Timestamp = DateTime.UtcNow;
                }
            }
            else
            {
                message.Timestamp = DateTime.UtcNow;
            }

            span = span.Slice(spaceIdx + 1);

            // -----------------------------------------------------------------
            // 2. HOSTNAME
            // -----------------------------------------------------------------

            spaceIdx = span.IndexOf(' ');

            // Message truncated during HOSTNAME.
            if (spaceIdx == -1)
                return message;

            var hostSpan = span.Slice(0, spaceIdx);

            // NILVALUE ("-") is treated as unset.
            if (hostSpan.Length > 0 && hostSpan[0] != '-')
                message.Hostname = hostSpan.ToString();

            span = span.Slice(spaceIdx + 1);

            // -----------------------------------------------------------------
            // 3. APP-NAME
            // -----------------------------------------------------------------

            spaceIdx = span.IndexOf(' ');

            // Message truncated during APP-NAME.
            if (spaceIdx == -1)
                return message;

            var appSpan = span.Slice(0, spaceIdx);

            // NILVALUE ("-") is treated as unset.
            if (appSpan.Length > 0 && appSpan[0] != '-')
                message.AppName = appSpan.ToString();

            span = span.Slice(spaceIdx + 1);

            // -----------------------------------------------------------------
            // 4. PROCID
            // -----------------------------------------------------------------

            spaceIdx = span.IndexOf(' ');

            // Message truncated during PROCID.
            if (spaceIdx == -1)
                return message;

            var procSpan = span.Slice(0, spaceIdx);

            // PROCID is optional and parsed only if numeric.
            if (procSpan.Length > 0 && procSpan[0] != '-')
            {
                if (int.TryParse(procSpan, out int pid))
                    message.ProcessId = pid;
            }

            span = span.Slice(spaceIdx + 1);

            // -----------------------------------------------------------------
            // 5. MSGID
            // -----------------------------------------------------------------
            // MSGID is currently skipped.
            // Parser only advances to the next token.

            spaceIdx = span.IndexOf(' ');

            // Message ends exactly at MSGID.
            if (spaceIdx == -1)
                return message;

            span = span.Slice(spaceIdx + 1);

            // -----------------------------------------------------------------
            // 6. STRUCTURED-DATA & MSG
            // -----------------------------------------------------------------
            // Handles:
            // [id1][id2][id3] Message
            //
            // Also tolerates:
            // - malformed structured-data
            // - compacted payloads
            // - missing spaces
            //
            // Parser skips structured-data blocks but does not fully parse
            // SD-ID or SD-PARAM values.

            if (span.Length > 0)
            {
                // Structured-data block(s)
                if (span[0] == '[')
                {
                    int currentIdx = 0;

                    // Safely advance through consecutive SD blocks.
                    while (currentIdx < span.Length && span[currentIdx] == '[')
                    {
                        int closingBracket =
                            span.Slice(currentIdx).IndexOf(']');

                        // Malformed SD block.
                        // Stop parsing gracefully.
                        if (closingBracket == -1)
                        {
                            message.Message = "";
                            return message;
                        }

                        currentIdx += closingBracket + 1;
                    }

                    // If a space follows the last SD block,
                    // everything after it is treated as MSG.
                    if (currentIdx < span.Length &&
                        span[currentIdx] == ' ')
                    {
                        message.Message =
                            span.Slice(currentIdx + 1).ToString();
                    }
                    // Handles compacted payloads without separator space.
                    else if (currentIdx < span.Length)
                    {
                        message.Message =
                            span.Slice(currentIdx).ToString();
                    }
                    else
                    {
                        message.Message = "";
                    }
                }
                // NILVALUE structured-data.
                else if (span[0] == '-')
                {
                    // Standard case:
                    // - Message
                    if (span.Length > 1 && span[1] == ' ')
                    {
                        message.Message = span.Slice(2).ToString();
                    }
                    // Handles compacted payloads like:
                    // -rsync started
                    else if (span.Length > 1)
                    {
                        message.Message = span.Slice(1).ToString();
                    }
                    else
                    {
                        message.Message = "";
                    }
                }
                // No structured-data present.
                else
                {
                    message.Message = span.ToString();
                }
            }

            return message;
        }
    }
}
