using SyslogHmi.Models;
using SyslogHmi.Services;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InvalidXmlDocComment
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace SyslogHmiTests.Services
{
    /// <summary>
    /// Tests for Rfc5424Parser class.
    /// </summary>
    public class Rfc5424ParserTests
    {
        /// <summary>
        /// Test for Parse method
        /// </summary>
        public class Parse
        {
            private readonly Rfc5424Parser _testedClass;

            /// <summary>
            /// Initialize a new instance of the Parse class.
            /// </summary>
            public Parse()
            {
                _testedClass = new Rfc5424Parser();
            }

            /// <summary>
            /// Verifies that passing a null, empty, or whitespace string returns null.
            /// </summary>
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public void WhenInputIsWhitespaceOrNullThenReturnNull(string? input)
            {
                var result = _testedClass.Parse(input!);
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that an input missing the opening priority bracket or has leading spaces returns null.
            /// </summary>
            [Theory]
            [InlineData("Short")]
            [InlineData("abc")]
            [InlineData(" <13>1")]
            public void WhenInputIsMalformedOrMissingOpeningBracketThenReturnNull(string input)
            {
                var result = _testedClass.Parse(input);
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that an input starting with bracket but too short to fulfill structure rules returns null.
            /// </summary>
            [Theory]
            [InlineData("<")]
            [InlineData("<1")]
            [InlineData("<13>")]
            public void WhenInputIsTooShortInternalCheckThenReturnNull(string input)
            {
                var result = _testedClass.Parse(input);
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that a message missing a closing priority bracket returns null.
            /// </summary>
            [Fact]
            public void WhenInputIsMissingClosingBracketThenReturnNull()
            {
                string input = "<131 2026-05-27T22:14:15Z host app - - msg";
                var result = _testedClass.Parse(input);
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that a non-numeric priority value inside brackets returns null.
            /// </summary>
            [Fact]
            public void WhenPriorityIsNonNumericThenReturnNull()
            {
                string input = "<ABC>1 2026-05-27T22:14:15Z host app - - msg";
                var result = _testedClass.Parse(input);
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that a priority total outside the standard limits safely maps to Unknown enums.
            /// </summary>
            [Fact]
            public void WhenPriorityIsOutOfBoundsThenMapToUnknownEnumsSafely()
            {
                string input = "<999>1 2026-05-27T22:14:15Z host app - - msg";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Unknown, result.Facility);
                Assert.Equal(SyslogSeverity.Unknown, result.Severity);
            }

            /// <summary>
            /// Verifies that a priority total matching zero evaluates as Kernel/Emergency.
            /// </summary>
            [Fact]
            public void WhenPriorityIsExactlyZeroThenProcessAsValidPriority()
            {
                string input = "<0>1 2026-05-27T22:14:15Z host app - - msg";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Kernel, result.Facility);
                Assert.Equal(SyslogSeverity.Emergency, result.Severity);
            }

            /// <summary>
            /// Verifies that a priority total matching a negative bounds converts safely to Unknown.
            /// </summary>
            [Fact]
            public void WhenPriorityIsNegativeThenMapToUnknownEnums()
            {
                string input = "<-10>1 2026-05-27T22:14:15Z host app - - msg";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Unknown, result.Facility);
                Assert.Equal(SyslogSeverity.Unknown, result.Severity);
            }

            /// <summary>
            /// Verifies short circuits when fields are abruptly truncated.
            /// </summary>
            [Theory]
            [InlineData("<13>1 2026-05-27T22:14:15Z")]
            [InlineData("<13>1 2026-05-27T22:14:15Z host")]
            [InlineData("<13>1 2026-05-27T22:14:15Z host app")]
            [InlineData("<13>1 2026-05-27T22:14:15Z host app 123")]
            public void WhenMessageIsTruncatedMidStreamThenReturnEarlyWithAvailableFields(string input)
            {
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
            }

            /// <summary>
            /// Verifies handling of corrupt or unparseable ISO Timestamps.
            /// </summary>
            [Fact]
            public void WhenTimestampIsCorruptThenFallbackToCurrentTime()
            {
                string input = "<13>1 InvalidTimestamp host app - - msg";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.True((DateTime.UtcNow - result.Timestamp).TotalSeconds < 5);
            }

            /// <summary>
            /// Verifies structured data corner case where brackets are left unclosed.
            /// </summary>
            [Fact]
            public void WhenStructuredDataIsUnclosedThenMessageIsEmpty()
            {
                string input = "<13>1 2026-05-27T22:14:15Z host app - - [exampleSDID@32473 i=\"1\"";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal("", result.Message);
            }

            /// <summary>
            /// Verifies structured data closing without trailing text or space.
            /// </summary>
            [Fact]
            public void WhenStructuredDataEndsExactlyAtLineEndThenMessageIsEmpty()
            {
                string input = "<13>1 2026-05-27T22:14:15Z host app - - [exampleSDID@32473]";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal("", result.Message);
            }

            /// <summary>
            /// Verifies non-numeric process IDs are ignored safely.
            /// </summary>
            [Fact]
            public void WhenProcIdIsNonNumericThenIgnoreAndLeaveZero()
            {
                string input = "<13>1 2026-05-27T22:14:15Z host app NOT_A_PID - - msg";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(0, result.ProcessId);
            }

            /// <summary>
            /// Verifies fallback when structured data space has no content message.
            /// </summary>
            [Fact]
            public void WhenNoStructuredDataAndLineEndsOnDashThenMessageIsEmpty()
            {
                string input = "<13>1 2026-05-27T22:14:15Z host app - - -";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal("", result.Message);
            }

            // --- PRODUCTION LOGS TESTS ---

            [Fact]
            public void WhenStandardProductionLog1GivenThenExtractFields()
            {
                string input = "<34>1 2003-10-11T22:14:15.003Z mymachine.example.com su - ID47 [exampleSDID@32473 iut=\"3\" eventSource=\"Application\" eventID=\"1011\"] 'su root' failed for lonvick on /dev/pts/8";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Auth, result.Facility);
                Assert.Equal(SyslogSeverity.Critical, result.Severity);
                Assert.Equal("mymachine.example.com", result.Hostname);
                Assert.Equal("su", result.AppName);
                Assert.Equal(0, result.ProcessId);
                Assert.Equal("'su root' failed for lonvick on /dev/pts/8", result.Message);
            }

            [Fact]
            public void WhenStandardProductionLog2GivenThenExtractFields()
            {
                string input = "<13>1 2026-05-27T17:32:18.123456Z web01 sshd 2145 - - Accepted password for admin from 192.168.1.50 port 54421";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.User, result.Facility);
                Assert.Equal(SyslogSeverity.Notice, result.Severity);
                Assert.Equal("web01", result.Hostname);
                Assert.Equal("sshd", result.AppName);
                Assert.Equal(2145, result.ProcessId);
                Assert.Equal("Accepted password for admin from 192.168.1.50 port 54421", result.Message);
            }

            [Fact]
            public void WhenProductionLogWithNoStructuredDataGivenThenExtractFields()
            {
                string input = "<165>1 2026-08-24T05:34:00Z router1 %LINK-3-UPDOWN - - - Interface GigabitEthernet0/1, changed state to up";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Local4, result.Facility);
                Assert.Equal(SyslogSeverity.Notice, result.Severity); // Corregido: 165 & 7 = 5 (Notice)
                Assert.Equal("router1", result.Hostname);
                Assert.Equal("%LINK-3-UPDOWN", result.AppName);
                Assert.Equal("Interface GigabitEthernet0/1, changed state to up", result.Message);
            }

            [Fact]
            public void WhenProductionLogWithNullValuesGivenThenExtractFields()
            {
                string input = "<14>1 - - - - - - (root) CMD (/usr/local/bin/backup.sh)";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.User, result.Facility);
                Assert.Equal(SyslogSeverity.Info, result.Severity);
                Assert.Equal("", result.Hostname);
                Assert.Equal("", result.AppName);
                Assert.Equal(0, result.ProcessId);
                Assert.Equal("(root) CMD (/usr/local/bin/backup.sh)", result.Message);
            }

            [Fact]
            public void WhenProductionLogWithMultipleStructuredBlocksGivenThenExtractFields()
            {
                string input = "<190>1 2026-03-15T12:45:33Z dbserver postgres 9981 - [meta sequence=\"1\"][sig valid=\"true\"] checkpoint complete";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Local7, result.Facility);
                Assert.Equal(SyslogSeverity.Info, result.Severity);
                Assert.Equal("dbserver", result.Hostname);
                Assert.Equal("postgres", result.AppName);
                Assert.Equal(9981, result.ProcessId);
                Assert.Equal("checkpoint complete", result.Message); // ¡Ahora sí pasará limpio!
            }

            [Fact]
            public void WhenProductionLogWithNoMessagePayloadGivenThenExtractFields()
            {
                string input = "<46>1 2026-04-07T09:22:11Z mailhost postfix/smtpd 4411 - [meta log=\"audit\"]";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal("mailhost", result.Hostname);
                Assert.Equal("postfix/smtpd", result.AppName);
                Assert.Equal(4411, result.ProcessId);
                Assert.Equal("", result.Message);
            }

            [Fact]
            public void WhenProductionLogAlternativeNoSdMessageGivenThenExtractFields()
            {
                string input = "<27>1 2026-09-18T07:55:02Z backup rsyncd 555 - -rsync error: alert packet received";
                var result = _testedClass.Parse(input);
                Assert.NotNull(result);
                Assert.Equal("rsync error: alert packet received", result.Message); // Extraído perfectamente tras el guion pegado
            }

            /// <summary>
            /// Verifies the short-circuit return when no space is found after the priority bracket,
            /// meaning the protocol version component is malformed or truncated.
            /// Covers: if (versionSpace == -1) return message;
            /// </summary>
            [Fact]
            public void WhenNoSpaceExistsAfterPriorityThenReturnEarlyWithPropertiesNull()
            {
                // Arrange - Length is 7 (passes < 7 check), but has no space after '>'
                string input = "<13>1234";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("", result.Hostname); // Returns early before parsing any structural components
            }

            /// <summary>
            /// Verifies the branch where hostSpan has length but starts with a dash character followed by text.
            /// Covers the false block of the second condition in: if (hostSpan.Length > 0 && hostSpan[0] != '-')
            /// </summary>
            [Fact]
            public void WhenHostnameStartsWithDashButHasLengthThenSkipAssignmentSafely()
            {
                // Arrange - Hostname is "-invalidhost" (starts with dash but length > 1)
                string input = "<13>1 2026-05-27T22:14:15Z -invalidhost app 123 ID45 - msg";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("", result.Hostname); // Should skip assignment because it starts with '-'
                Assert.Equal("app", result.AppName); // Verifies parsing continued smoothly for the rest
            }

            /// <summary>
            /// Verifies the short-circuit return when the message terminates exactly during the MSGID field
            /// with no trailing space or message payload.
            /// Covers: if (spaceIdx == -1) return message; (after PROCID)
            /// </summary>
            [Fact]
            public void WhenMessageEndsAbruptlyAtMsgIdThenReturnEarlyWithPropertiesNull()
            {
                // Arrange - Ends exactly at "MYMSGID" with no spaces after it
                string input = "<13>1 2026-05-27T22:14:15Z host app 123 MYMSGID";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal(123, result.ProcessId);
                Assert.Equal("", result.Message); // Truncated before MSG, evaluates to empty string
            }

            /// <summary>
            /// Verifies the branch where a text payload exists immediately after the closing structured data bracket
            /// without any mandatory separating space.
            /// Covers: else if (currentIdx < span.Length) -> message.Message = span.Slice(currentIdx).ToString();
            /// </summary>
            [Fact]
            public void WhenPayloadIsGluedToStructuredDataBracketThenExtractPayloadCleanly()
            {
                // Arrange - The message text "GluedMessageContent" is attached directly to ']'
                string input = "<13>1 2026-05-27T22:14:15Z host app 123 ID45 [meta seq=\"1\"]GluedMessageContent";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host", result.Hostname);
                Assert.Equal("GluedMessageContent", result.Message); // Successfully extracts the text from the edge
            }

            /// <summary>
            /// Verifies parsing of a minimal valid RFC5424 message.
            /// Covers: happy path with NILVALUE structured-data.
            /// </summary>
            [Fact]
            public void WhenParsingMinimalValidMessageThenFieldsAreParsedCorrectly()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:00Z host1 app - - - Test message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host1", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal(0, result.ProcessId);
                Assert.Equal("Test message", result.Message);
            }

            /// <summary>
            /// Verifies parsing of structured-data with one SD-ID.
            /// Covers: structured-data extraction.
            /// </summary>
            [Fact]
            public void WhenParsingStructuredDataThenStructuredDataIsPreserved()
            {
                // Arrange
                string input = "<165>1 2026-05-27T21:15:01Z router1 bgpd 1024 ID47 [exampleSDID@32473 iut=\"3\"] Neighbor established";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("router1", result.Hostname);
                Assert.Equal("bgpd", result.AppName);
                Assert.Equal(1024, result.ProcessId);
                Assert.Equal("Neighbor established", result.Message);
            }

            /// <summary>
            /// Verifies parsing of multiple structured-data elements.
            /// Covers: consecutive SD blocks.
            /// </summary>
            [Fact]
            public void WhenParsingMultipleStructuredDataBlocksThenMessageIsParsed()
            {
                // Arrange
                string input = "<13>1 2026-05-27T21:15:02Z web01 nginx 222 access01 [meta@123 env=\"prod\"][trace@456 traceId=\"abc123\"] GET /index.html 200";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("web01", result.Hostname);
                Assert.Equal("nginx", result.AppName);
                Assert.Equal(222, result.ProcessId);
                Assert.Equal("GET /index.html 200", result.Message);
            }

            /// <summary>
            /// Verifies NILVALUE hostname parsing.
            /// Covers: hostname = "-".
            /// </summary>
            [Fact]
            public void WhenHostnameIsNilValueThenHostnameIsNull()
            {
                // Arrange
                string input = "<11>1 2026-05-27T21:15:03Z - sshd 9812 ID99 - Accepted publickey";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Empty(result.Hostname);
                Assert.Equal("sshd", result.AppName);
                Assert.Equal(9812, result.ProcessId);
                Assert.Equal("Accepted publickey", result.Message);
            }

            /// <summary>
            /// Verifies NILVALUE app-name parsing.
            /// Covers: app-name = "-".
            /// </summary>
            [Fact]
            public void WhenAppNameIsNilValueThenAppNameIsNull()
            {
                // Arrange
                string input = "<14>1 2026-05-27T21:15:04Z db01 - 4412 ID100 - Database startup complete";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("db01", result.Hostname);
                Assert.Empty(result.AppName);
                Assert.Equal(4412, result.ProcessId);
                Assert.Equal("Database startup complete", result.Message);
            }

            /// <summary>
            /// Verifies NILVALUE procid parsing.
            /// Covers: procid = "-".
            /// </summary>
            [Fact]
            public void WhenProcessIdIsNilValueThenProcessIdIsNull()
            {
                // Arrange
                string input = "<22>1 2026-05-27T21:15:05Z cache01 redis - ID101 - Ready to accept connections";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("cache01", result.Hostname);
                Assert.Equal("redis", result.AppName);
                Assert.Equal(0, result.ProcessId);
                Assert.Equal("Ready to accept connections", result.Message);
            }

            /// <summary>
            /// Verifies NILVALUE msgid parsing.
            /// Covers: msgid = "-".
            /// </summary>
            [Fact]
            public void WhenMsgIdIsNilValueThenParsingStillSucceeds()
            {
                // Arrange
                string input = "<27>1 2026-05-27T21:15:06Z proxy01 squid 999 - - TCP_MISS/200";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("proxy01", result.Hostname);
                Assert.Equal("squid", result.AppName);
                Assert.Equal(999, result.ProcessId);
                Assert.Equal("TCP_MISS/200", result.Message);
            }

            /// <summary>
            /// Verifies empty MSG parsing.
            /// Covers: no MSG payload after structured-data.
            /// </summary>
            [Fact]
            public void WhenMessagePayloadIsMissingThenMessageIsEmpty()
            {
                // Arrange
                string input = "<38>1 2026-05-27T21:15:07Z sensor01 telemetry 77 DATA01 -";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("sensor01", result.Hostname);
                Assert.Equal("telemetry", result.AppName);
                Assert.Equal(77, result.ProcessId);
                Assert.Equal(string.Empty, result.Message);
            }

            /// <summary>
            /// Verifies UTF8 content parsing.
            /// Covers: unicode message body.
            /// </summary>
            [Fact]
            public void WhenMessageContainsUtf8ThenUtf8IsPreserved()
            {
                // Arrange
                string input = "<46>1 2026-05-27T21:15:08Z app01 java 1234 EVT100 - Servicio iniciado correctamente";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("Servicio iniciado correctamente", result.Message);
            }

            /// <summary>
            /// Verifies escaped characters inside structured-data.
            /// Covers: escaped quotes and backslashes.
            /// </summary>
            [Fact]
            public void WhenStructuredDataContainsEscapedCharactersThenParsingSucceeds()
            {
                // Arrange
                string input = "<91>1 2026-05-27T21:15:09Z auth01 login 555 AUTHFAIL [auth@32473 user=\"bob\" path=\"C:\\\\Users\\\\bob\"] Login failed";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("auth01", result.Hostname);
                Assert.Equal("login", result.AppName);
                Assert.Equal(555, result.ProcessId);
                Assert.Equal("Login failed", result.Message);
            }

            /// <summary>
            /// Verifies structured-data with empty parameter values.
            /// Covers: sd-param empty string.
            /// </summary>
            [Fact]
            public void WhenStructuredDataContainsEmptyParameterThenParsingSucceeds()
            {
                // Arrange
                string input = "<134>1 2026-05-27T21:15:10Z fw01 kernel 777 FIREWALL [fw@32473 dst=\"\"] Packet dropped";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("fw01", result.Hostname);
                Assert.Equal("kernel", result.AppName);
                Assert.Equal(777, result.ProcessId);
                Assert.Equal("Packet dropped", result.Message);
            }

            /// <summary>
            /// Verifies IPv4 hostname parsing.
            /// Covers: hostname as IPv4.
            /// </summary>
            [Fact]
            public void WhenHostnameIsIpv4ThenParsingSucceeds()
            {
                // Arrange
                string input = "<78>1 2026-05-27T21:15:11Z 192.168.1.10 monitor 88 STATUS - CPU threshold exceeded";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("192.168.1.10", result.Hostname);
                Assert.Equal("monitor", result.AppName);
                Assert.Equal(88, result.ProcessId);
            }

            /// <summary>
            /// Verifies IPv6 hostname parsing.
            /// Covers: hostname as IPv6.
            /// </summary>
            [Fact]
            public void WhenHostnameIsIpv6ThenParsingSucceeds()
            {
                // Arrange
                string input = "<5>1 2026-05-27T21:15:12Z 2001:db8::1 sensor 12 TEMP - Temperature warning";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("2001:db8::1", result.Hostname);
                Assert.Equal("sensor", result.AppName);
                Assert.Equal(12, result.ProcessId);
            }

            /// <summary>
            /// Verifies timezone offset timestamp parsing.
            /// Covers: timestamps with timezone offsets.
            /// </summary>
            [Fact]
            public void WhenTimestampContainsTimezoneOffsetThenParsingSucceeds()
            {
                // Arrange
                string input = "<190>1 2026-05-27T23:15:13+02:00 eu-app api 3000 REQ001 - User login request";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("eu-app", result.Hostname);
                Assert.Equal("api", result.AppName);
                Assert.Equal(3000, result.ProcessId);
            }

            /// <summary>
            /// Verifies fractional seconds parsing.
            /// Covers: sub-second precision timestamps.
            /// </summary>
            [Fact]
            public void WhenTimestampContainsFractionalSecondsThenParsingSucceeds()
            {
                // Arrange
                string input = "<4>1 2026-05-27T21:15:14.123456Z collector ingest 5555 EVT42 - Event processed";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("collector", result.Hostname);
                Assert.Equal("ingest", result.AppName);
                Assert.Equal(5555, result.ProcessId);
            }

            /// <summary>
            /// Verifies maximum valid PRI parsing.
            /// Covers: PRI = 191.
            /// </summary>
            [Fact]
            public void WhenPriorityIsMaximumThenParsingSucceeds()
            {
                // Arrange
                string input = "<191>1 2026-05-27T21:15:15Z critical-host kernel 1 PANIC - Kernel panic detected";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("critical-host", result.Hostname);
                Assert.Equal("kernel", result.AppName);
                Assert.Equal(1, result.ProcessId);
            }

            /// <summary>
            /// Verifies parser tolerance for invalid RFC5424 version values.
            /// Covers: best-effort parsing when VERSION = 0.
            /// </summary>
            [Fact]
            public void WhenVersionIsInvalidThenParserStillExtractsFields()
            {
                // Arrange
                string input = "<34>0 2026-05-27T21:15:16Z host1 app 1 TEST - Invalid version";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host1", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal(1, result.ProcessId);
                Assert.Equal("Invalid version", result.Message);
            }

            /// <summary>
            /// Verifies parser tolerance for malformed timestamps.
            /// Covers: best-effort parsing when TIMESTAMP is invalid.
            /// </summary>
            [Fact]
            public void WhenTimestampIsInvalidThenParserStillExtractsFields()
            {
                // Arrange
                string input = "<34>1 NOT_A_TIMESTAMP host1 parser 1 TEST - Invalid timestamp";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host1", result.Hostname);
                Assert.Equal("parser", result.AppName);
                Assert.Equal(1, result.ProcessId);
                Assert.Equal("Invalid timestamp", result.Message);
            }

            /// <summary>
            /// Verifies parser tolerance for malformed structured-data.
            /// Covers: best-effort parsing when SD block is malformed.
            /// </summary>
            [Fact]
            public void WhenStructuredDataIsMalformedThenParserStillExtractsFields()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:17Z host1 app 1 TEST [brokenSD foo=\"bar\"] Broken SD";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host1", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal(1, result.ProcessId);

                // Depending on implementation, malformed SD may end up inside MSG.
                Assert.Contains("Broken SD", result.Message);
            }

            /// <summary>
            /// Verifies very long message payload parsing.
            /// Covers: large MSG body.
            /// </summary>
            [Fact]
            public void WhenMessagePayloadIsVeryLongThenParsingSucceeds()
            {
                // Arrange
                string input = "<23>1 2026-05-27T21:15:18Z loghost aggregator 4242 BULK - " +
                               "Lorem ipsum dolor sit amet, consectetur adipiscing elit," +
                               "sed do eiusmod tempor incididunt ut labore et dolore magna aliqua." +
                               "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris" +
                               "nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in" +
                               "reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla" +
                               "pariatur. Excepteur sint occaecat cupidatat non proident, sunt in" +
                               "culpa qui officia deserunt mollit anim id est laborum.";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("loghost", result.Hostname);
                Assert.Equal("aggregator", result.AppName);
                Assert.Equal(4242, result.ProcessId);
                Assert.StartsWith("Lorem ipsum", result.Message);
            }

            /// <summary>
            /// Verifies parser rejects null input.
            /// Covers: string.IsNullOrWhiteSpace.
            /// </summary>
            [Fact]
            public void WhenInputIsNullThenReturnNull()
            {
                // Arrange
                string? input = null;

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies parser rejects empty input.
            /// Covers: empty string.
            /// </summary>
            [Fact]
            public void WhenInputIsEmptyThenReturnNull()
            {
                // Arrange
                string input = "";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies parser rejects whitespace-only input.
            /// Covers: whitespace validation.
            /// </summary>
            [Fact]
            public void WhenInputIsWhitespaceThenReturnNull()
            {
                // Arrange
                string input = "     ";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies parser rejects messages not starting with PRI.
            /// Covers: missing opening bracket.
            /// </summary>
            [Fact]
            public void WhenMessageDoesNotStartWithPriThenReturnNull()
            {
                // Arrange
                string input = "34>1 2026-05-27T21:15:00Z host app 1 ID - Message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies parser rejects messages shorter than minimum size.
            /// Covers: textSpan.Length < 7.
            /// </summary>
            [Fact]
            public void WhenMessageIsTooShortThenReturnNull()
            {
                // Arrange
                string input = "<1>x";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies parser rejects missing PRI closing bracket.
            /// Covers: priEnd == -1.
            /// </summary>
            [Fact]
            public void WhenPriClosingBracketIsMissingThenReturnNull()
            {
                // Arrange
                string input = "<34 1 2026-05-27T21:15:00Z host app 1 ID - Message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies parser rejects non-numeric PRI.
            /// Covers: int.TryParse(priSpan).
            /// </summary>
            [Fact]
            public void WhenPriIsNotNumericThenReturnNull()
            {
                // Arrange
                string input = "<abc>1 2026-05-27T21:15:00Z host app 1 ID - Message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies parser tolerates PRI values outside RFC range.
            /// Covers: invalid PRI fallback behavior.
            /// </summary>
            [Fact]
            public void WhenPriIsOutOfRangeThenParsingStillSucceeds()
            {
                // Arrange
                string input = "<999>1 2026-05-27T21:15:00Z host app 1 ID - Message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal(1, result.ProcessId);
                Assert.Equal("Message", result.Message);
            }

            /// <summary>
            /// Verifies parser ignores non-numeric PROCID.
            /// Covers: PROCID parse failure.
            /// </summary>
            [Fact]
            public void WhenProcessIdIsNonNumericThenProcessIdRemainsNull()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:00Z host app abc MSGID - Message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);

                Assert.Equal("host", result.Hostname);
                Assert.Equal("app", result.AppName);

                // PROCID parsing fails gracefully.
                Assert.Equal(0, result.ProcessId);

                Assert.Equal("Message", result.Message);
            }

            /// <summary>
            /// Verifies parser handles malformed structured-data without closing bracket.
            /// Covers: closingBracket == -1.
            /// </summary>
            [Fact]
            public void WhenStructuredDataClosingBracketIsMissingThenMessageIsEmpty()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:00Z host app 1 MSGID [brokenSD Message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("", result.Message);
            }

            /// <summary>
            /// Verifies parser handles compacted structured-data payloads.
            /// Covers: no space after structured-data.
            /// </summary>
            [Fact]
            public void WhenStructuredDataAndMessageAreCompactedThenMessageIsExtracted()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:00Z host app 1 MSGID [meta]CompactedMessage";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("CompactedMessage", result.Message);
            }

            /// <summary>
            /// Verifies parser handles compacted NILVALUE payloads.
            /// Covers: '-message' compacted format.
            /// </summary>
            [Fact]
            public void WhenNilValueAndMessageAreCompactedThenMessageIsExtracted()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:00Z host app 1 MSGID -CompactedMessage";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("CompactedMessage", result.Message);
            }

            /// <summary>
            /// Verifies parser handles message with only NILVALUE structured-data.
            /// Covers: structured-data only.
            /// </summary>
            [Fact]
            public void WhenStructuredDataIsOnlyNilValueThenMessageIsEmpty()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:00Z host app 1 MSGID -";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("", result.Message);
            }

            /// <summary>
            /// Verifies parser preserves raw unstructured payloads.
            /// Covers: fallback branch.
            /// </summary>
            [Fact]
            public void WhenPayloadDoesNotStartWithStructuredDataThenEntirePayloadIsMessage()
            {
                // Arrange
                string input = "<34>1 2026-05-27T21:15:00Z host app 1 MSGID RAW_PAYLOAD";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("RAW_PAYLOAD", result.Message);
            }
        }
    }
}