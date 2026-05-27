using SyslogHmi.Models;
using SyslogHmi.Services;
// ReSharper disable StringLiteralTypo
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InvalidXmlDocComment
// ReSharper disable IdentifierTypo

namespace SyslogHmiTests.Services
{
    /// <summary>
    /// Tests for Rfc3164Parser class.
    /// </summary>
    public class Rfc3164ParserTests
    {
        /// <summary>
        /// Test for Parse method
        /// </summary>
        public class Parse
        {
            private readonly ISyslogParser _testedClass;

            /// <summary>
            /// Initialize a new instance of the Parse class.
            /// </summary>
            public Parse()
            {
                _testedClass = new Rfc3164Parser();
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
                // Act
                var result = _testedClass.Parse(input!);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that an input missing the opening priority bracket or has leading spaces returns null.
            /// </summary>
            [Theory]
            [InlineData("Short")]
            [InlineData("abc")]
            [InlineData(" <13>")] // Will now pass successfully because rawMessage[0] == ' ' returns null!
            public void WhenInputIsMalformedOrMissingOpeningBracketThenReturnNull(string input)
            {
                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that a message missing a closing priority bracket returns null.
            /// </summary>
            [Fact]
            public void WhenInputIsMissingClosingBracketThenReturnNull()
            {
                // Arrange
                var input = "<13Oct 11 22:14:15 hostname app: message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that a non-numeric priority value inside brackets returns null.
            /// </summary>
            [Fact]
            public void WhenPriorityIsNonNumericThenReturnNull()
            {
                // Arrange
                var input = "<ABC>Oct 11 22:14:15 hostname app: message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that a message containing only a valid priority returns basic enums and empty properties.
            /// </summary>
            [Fact]
            public void WhenPayloadAfterPriorityIsEmptyThenReturnMessageWithMappedEnumsAndNullProperties()
            {
                // Arrange (13 -> Facility: User, Severity: Notice)
                var input = "<13>";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.User, result.Facility);
                Assert.Equal(SyslogSeverity.Notice, result.Severity);
            }

            /// <summary>
            /// Verifies that a payload shorter than the standard 16 characters defaults to current time and puts everything in the Message.
            /// </summary>
            [Fact]
            public void WhenPayloadAfterPriorityIsLessThan16CharactersThenFallbackToCurrentTimeAndRawPayloadMessage()
            {
                // Arrange
                var input = "<13>Short payload";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.True((DateTime.UtcNow - result.Timestamp).TotalSeconds < 5);
                Assert.Equal("Short payload", result.Message);
            }

            /// <summary>
            /// Verifies that a malformed timestamp defaults to the current system time while continuing to parse the remaining components.
            /// </summary>
            [Fact]
            public void WhenTimestampIsCorruptThenFallbackToCurrentTimeAndContinueParsing()
            {
                // Arrange ("Not A Timestamp " is exactly 16 chars)
                var input = "<13>Not A Timestamp hostname app: message content";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.True((DateTime.UtcNow - result.Timestamp).TotalSeconds < 5);
                Assert.Equal("hostname", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal("message content", result.Message);
            }

            /// <summary>
            /// Verifies that a valid timestamp followed by no whitespace returns the parsed date with no other properties.
            /// </summary>
            [Fact]
            public void WhenInputEndsImmediatelyAfterTimestampThenReturnMessageWithoutHostnameOrAppName()
            {
                // Arrange
                var input = "<13>Oct 11 22:14:15";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(DateTime.UtcNow.Year, result.Timestamp.Year);
                Assert.Equal(10, result.Timestamp.Month); // Expected: 10 (October) -> Will now be Actual: 10!
                Assert.Equal(11, result.Timestamp.Day);
            }

            /// <summary>
            /// Verifies that a log line missing a colon falls back to treating everything after the hostname as the message content.
            /// </summary>
            [Fact]
            public void WhenInputIsMissingColonDelimiterThenFallbackRemainderToMessageContent()
            {
                // Arrange
                var input = "<13>Oct 11 22:14:15 host-no-colon raw text message line here";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host-no-colon", result.Hostname);
                Assert.Equal("raw text message line here", result.Message);
            }

            /// <summary>
            /// Verifies that a standard compliant message structure is correctly decomposed into all expected fields.
            /// </summary>
            [Fact]
            public void WhenMessageHasStandardColonAndSpacePatternThenExtractAllFieldsCorrectly()
            {
                // Arrange
                var input = "<14>Oct 11 22:14:15 web-server nginx: Processed request in 15ms";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.User, result.Facility);
                Assert.Equal(SyslogSeverity.Info, result.Severity);
                Assert.Equal("web-server", result.Hostname);
                Assert.Equal("nginx", result.AppName);
                Assert.Equal(0, result.ProcessId); // Changed from Assert.Null to match value type default
                Assert.Equal("Processed request in 15ms", result.Message);
            }

            /// <summary>
            /// Verifies that a message without a space right after the colon is still parsed correctly.
            /// </summary>
            [Fact]
            public void WhenMessageHasColonButNoTrailingSpaceThenExtractAllFieldsCorrectly()
            {
                // Arrange
                var input = "<14>Oct 11 22:14:15 web-server nginx:error-message-payload";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("web-server", result.Hostname);
                Assert.Equal("nginx", result.AppName);
                Assert.Equal("error-message-payload", result.Message);
            }

            /// <summary>
            /// Verifies that numeric brackets in the tag are isolated and assigned as the ProcessId.
            /// </summary>
            [Fact]
            public void WhenMessageContainsProcessIdBracketsThenExtractAppNameAndPidCorrectly()
            {
                // Arrange
                var input = "<34>Oct 11 22:14:15 debian-node custom-daemon[8845]: Service started";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Auth, result.Facility);
                Assert.Equal(SyslogSeverity.Critical, result.Severity);
                Assert.Equal("debian-node", result.Hostname);
                Assert.Equal("custom-daemon", result.AppName);
                Assert.Equal(8845, result.ProcessId);
                Assert.Equal("Service started", result.Message);
            }

            /// <summary>
            /// Verifies that a tag containing an unclosed bracket skips the ProcessId parsing step.
            /// </summary>
            [Fact]
            public void WhenMessageContainsMalformedProcessIdBracketsThenKeepWholeTagAsAppNameAndSkipPid()
            {
                // Arrange
                var input = "<34>Oct 11 22:14:15 debian-node custom-daemon[8845: Service started";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("debian-node", result.Hostname);
                Assert.Equal("custom-daemon[8845", result.AppName);
                Assert.Equal(0, result.ProcessId); // Verified as default integer value 0
                Assert.Equal("Service started", result.Message);
            }

            /// <summary>
            /// Verifies that a tag containing non-numeric values inside the tracking bracket skips the ProcessId parsing step safely.
            /// </summary>
            [Fact]
            public void WhenMessageContainsNonNumericProcessIdBracketsThenKeepWholeTagAsAppNameAndSkipPid()
            {
                // Arrange
                var input = "<34>Oct 11 22:14:15 debian-node custom-daemon[PID123]: Service started";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("debian-node", result.Hostname);
                Assert.Equal("custom-daemon", result.AppName);
                Assert.Equal(0, result.ProcessId); // Verified as default integer value 0
                Assert.Equal("Service started", result.Message);
            }

            /// <summary>
            /// Verifies that a priority total outside the standard limits safely maps to Unknown enums.
            /// </summary>
            [Fact]
            public void WhenPriorityIsOutOfBoundsThenMapToUnknownEnumsSafely()
            {
                // Arrange
                var input = "<999>Oct 11 22:14:15 host app: message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Unknown, result.Facility);
                Assert.Equal(SyslogSeverity.Unknown, result.Severity);
            }

            [Fact]
            public void WhenSuLogGivenThenExtractFields()
            {
                var input = "<34>Oct 11 22:14:15 mymachine su: 'su root' failed for lonvick on /dev/pts/8";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Auth, result.Facility);      // 34 >> 3 = 4
                Assert.Equal(SyslogSeverity.Critical, result.Severity);  // 34 & 7 = 2
                Assert.Equal("mymachine", result.Hostname);
                Assert.Equal("su", result.AppName);
                Assert.Equal(0, result.ProcessId);
                Assert.Equal("'su root' failed for lonvick on /dev/pts/8", result.Message);
            }

            [Fact]
            public void WhenSshdLogWithDoubleSpaceDayGivenThenExtractFields()
            {
                var input = "<13>Feb  5 17:32:18 web01 sshd[2145]: Accepted password for admin from 192.168.1.50 port 54421 ssh2";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.User, result.Facility);      // 13 >> 3 = 1
                Assert.Equal(SyslogSeverity.Notice, result.Severity);    // 13 & 7 = 5
                Assert.Equal(2, result.Timestamp.Month);                 // Feb
                Assert.Equal(5, result.Timestamp.Day);
                Assert.Equal("web01", result.Hostname);
                Assert.Equal("sshd", result.AppName);
                Assert.Equal(2145, result.ProcessId);
                Assert.Equal("Accepted password for admin from 192.168.1.50 port 54421 ssh2", result.Message);
            }

            [Fact]
            public void WhenCiscoNetworkLogGivenThenExtractFields()
            {
                var input = "<165>Aug 24 05:34:00 router1 %LINK-3-UPDOWN: Interface GigabitEthernet0/1, changed state to up";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Local4, result.Facility);    // 165 >> 3 = 20
                Assert.Equal(SyslogSeverity.Notice, result.Severity);    // 165 & 7 = 5
                Assert.Equal("router1", result.Hostname);
                Assert.Equal("%LINK-3-UPDOWN", result.AppName);
                Assert.Equal("Interface GigabitEthernet0/1, changed state to up", result.Message);
            }

            [Fact]
            public void WhenKernelFirewallLogGivenThenExtractFields()
            {
                var input = "<4>Jan  1 00:00:01 firewall kernel: IN=eth0 OUT= MAC=00:11:22:33:44:55 SRC=10.0.0.5 DST=10.0.0.1 LEN=60";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Kernel, result.Facility);    // 4 >> 3 = 0
                Assert.Equal(SyslogSeverity.Warning, result.Severity);   // 4 & 7 = 4
                Assert.Equal(1, result.Timestamp.Month);                 // Jan
                Assert.Equal(1, result.Timestamp.Day);
                Assert.Equal("firewall", result.Hostname);
                Assert.Equal("kernel", result.AppName);
                Assert.Equal("IN=eth0 OUT= MAC=00:11:22:33:44:55 SRC=10.0.0.5 DST=10.0.0.1 LEN=60", result.Message);
            }

            [Fact]
            public void WhenPostgresLogWithPercentageGivenThenExtractFields()
            {
                var input = "<190>Mar 15 12:45:33 dbserver postgres[9981]: checkpoint complete: wrote 1523 buffers (9.3%)";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Local7, result.Facility);    // 190 >> 3 = 23
                Assert.Equal(SyslogSeverity.Info, result.Severity);      // 190 & 7 = 6
                Assert.Equal("dbserver", result.Hostname);
                Assert.Equal("postgres", result.AppName);
                Assert.Equal(9981, result.ProcessId);
                Assert.Equal("checkpoint complete: wrote 1523 buffers (9.3%)", result.Message);
            }

            [Fact]
            public void WhenPostfixLogWithSlashInTagGivenThenExtractFields()
            {
                var input = "<46>Apr  7 09:22:11 mailhost postfix/smtpd[4411]: connect from unknown[203.0.113.9]";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal("mailhost", result.Hostname);
                Assert.Equal("postfix/smtpd", result.AppName);
                Assert.Equal(4411, result.ProcessId);
                Assert.Equal("connect from unknown[203.0.113.9]", result.Message);
            }

            [Fact]
            public void WhenJavaExceptionLogGivenThenExtractFields()
            {
                var input = "<78>May 30 16:01:44 app01 java[882]: ERROR NullPointerException at com.example.service.UserService";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Cron, result.Facility);      // 78 >> 3 = 9
                Assert.Equal(SyslogSeverity.Info, result.Severity);    // 78 & 7 = 6
                Assert.Equal("app01", result.Hostname);
                Assert.Equal("java", result.AppName);
                Assert.Equal(882, result.ProcessId);
                Assert.Equal("ERROR NullPointerException at com.example.service.UserService", result.Message);
            }

            [Fact]
            public void WhenNamedDnsLogWithHashGivenThenExtractFields()
            {
                var input = "<11>Jun  9 03:14:15 dns01 named[712]: client @0x7f8c2c0012a0 192.0.2.44#33333: query failed (SERVFAIL)";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal("dns01", result.Hostname);
                Assert.Equal("named", result.AppName);
                Assert.Equal(712, result.ProcessId);
                Assert.Equal("client @0x7f8c2c0012a0 192.0.2.44#33333: query failed (SERVFAIL)", result.Message);
            }

            [Fact]
            public void WhenSquidProxyLogGivenThenExtractFields()
            {
                var input = "<134>Jul 20 20:20:20 proxy01 squid[31337]: TCP_MISS/200 1045 GET http://example.com/ - HIER_DIRECT/93.184.216.34";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Local0, result.Facility);    // 134 >> 3 = 16
                Assert.Equal(SyslogSeverity.Info, result.Severity);   // 134 & 7 = 6
                Assert.Equal("proxy01", result.Hostname);
                Assert.Equal("squid", result.AppName);
                Assert.Equal(31337, result.ProcessId);
                Assert.Equal("TCP_MISS/200 1045 GET http://example.com/ - HIER_DIRECT/93.184.216.34", result.Message);
            }

            [Fact]
            public void WhenRsyncLogGivenThenExtractFields()
            {
                var input = "<27>Sep 18 07:55:02 backup rsyncd[555]: rsync error: received SIGINT, SIGTERM, or SIGHUP (code 20)";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Daemon, result.Facility);    // 27 >> 3 = 3
                Assert.Equal(SyslogSeverity.Error, result.Severity);    // 27 & 7 = 3
                Assert.Equal("backup", result.Hostname);
                Assert.Equal("rsyncd", result.AppName);
                Assert.Equal(555, result.ProcessId);
                Assert.Equal("rsync error: received SIGINT, SIGTERM, or SIGHUP (code 20)", result.Message);
            }

            [Fact]
            public void WhenCronJobLogGivenThenExtractFields()
            {
                var input = "<14>Oct 31 23:59:59 localhost cron[1001]: (root) CMD (/usr/local/bin/backup.sh)";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.User, result.Facility);
                Assert.Equal(SyslogSeverity.Info, result.Severity);
                Assert.Equal("localhost", result.Hostname);
                Assert.Equal("cron", result.AppName);
                Assert.Equal(1001, result.ProcessId);
                Assert.Equal("(root) CMD (/usr/local/bin/backup.sh)", result.Message);
            }

            [Fact]
            public void WhenSwitchStpLogGivenThenExtractFields()
            {
                var input = "<22>Nov 11 11:11:11 switch-7 STP[77]: Topology Change Notice received on port 12";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Mail, result.Facility);      // 22 >> 3 = 2
                Assert.Equal(SyslogSeverity.Info, result.Severity);      // 22 & 7 = 6
                Assert.Equal("switch-7", result.Hostname);
                Assert.Equal("STP", result.AppName);
                Assert.Equal(77, result.ProcessId);
                Assert.Equal("Topology Change Notice received on port 12", result.Message);
            }

            [Fact]
            public void WhenHypervisorCpuThrottleLogGivenThenExtractFields()
            {
                var input = "<3>Dec 25 06:30:45 hypervisor kernel: CPU0: Temperature above threshold, cpu clock throttled";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Kernel, result.Facility);    // 3 >> 3 = 0
                Assert.Equal(SyslogSeverity.Error, result.Severity);     // 3 & 7 = 3
                Assert.Equal("hypervisor", result.Hostname);
                Assert.Equal("kernel", result.AppName);
                Assert.Equal("CPU0: Temperature above threshold, cpu clock throttled", result.Message);
            }

            [Fact]
            public void WhenSambaSmbdLogGivenThenExtractFields()
            {
                var input = "<91>Jan 12 10:08:00 nas01 smbd[2201]: Authentication for user [alice] -> [alice] FAILED with error NT_STATUS_WRONG_PASSWORD";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Ftp, result.Facility);       // 91 >> 3 = 11
                Assert.Equal(SyslogSeverity.Error, result.Severity);  // 91 & 7 = 3
                Assert.Equal("nas01", result.Hostname);
                Assert.Equal("smbd", result.AppName);
                Assert.Equal(2201, result.ProcessId);
                Assert.Equal("Authentication for user [alice] -> [alice] FAILED with error NT_STATUS_WRONG_PASSWORD", result.Message);
            }

            [Fact]
            public void WhenIotMqttLogWithoutPidGivenThenExtractFields()
            {
                var input = "<38>Feb 28 14:17:09 iot-gateway mqttd: client sensor-44 disconnected unexpectedly";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Auth, result.Facility);
                Assert.Equal(SyslogSeverity.Info, result.Severity);
                Assert.Equal("iot-gateway", result.Hostname);
                Assert.Equal("mqttd", result.AppName);
                Assert.Equal(0, result.ProcessId);
                Assert.Equal("client sensor-44 disconnected unexpectedly", result.Message);
            }

            [Fact]
            public void WhenPrinterLpLogGivenThenExtractFields()
            {
                var input = "<5>Mar  3 03:03:03 printer lp: Paper jam detected in tray 2";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Kernel, result.Facility);
                Assert.Equal(SyslogSeverity.Notice, result.Severity);
                Assert.Equal("printer", result.Hostname);
                Assert.Equal("lp", result.AppName);
                Assert.Equal("Paper jam detected in tray 2", result.Message);
            }

            [Fact]
            public void WhenBgpRoutingLogGivenThenExtractFields()
            {
                var input = "<166>Apr 21 18:44:55 edge-router bgpd[912]: neighbor 203.0.113.1 Down BGP Notification sent";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Local4, result.Facility);    // 166 >> 3 = 20
                Assert.Equal(SyslogSeverity.Info, result.Severity);    // 166 & 7 = 6
                Assert.Equal("edge-router", result.Hostname);
                Assert.Equal("bgpd", result.AppName);
                Assert.Equal(912, result.ProcessId);
                Assert.Equal("neighbor 203.0.113.1 Down BGP Notification sent", result.Message);
            }

            [Fact]
            public void WhenSystemUnusableEmergencyLogGivenThenExtractFields()
            {
                var input = "<0>May  1 00:00:00 emergency-host init: System is unusable";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Kernel, result.Facility);
                Assert.Equal(SyslogSeverity.Emergency, result.Severity);
                Assert.Equal("emergency-host", result.Hostname);
                Assert.Equal("init", result.AppName);
                Assert.Equal("System is unusable", result.Message);
            }

            [Fact]
            public void WhenCustomTagWithDashesAndPayloadGivenThenExtractFields()
            {
                var input = "<15>Jun 10 13:13:13 testbox custom-tag-with-dashes[42]: payload=\"quoted value\" user=bob action=login-success";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.User, result.Facility);
                Assert.Equal(SyslogSeverity.Debug, result.Severity);
                Assert.Equal("testbox", result.Hostname);
                Assert.Equal("custom-tag-with-dashes", result.AppName);
                Assert.Equal(42, result.ProcessId);
                Assert.Equal("payload=\"quoted value\" user=bob action=login-success", result.Message);
            }

            [Fact]
            public void WhenMalformedHostWithNoColonOrTagGivenThenFallbackMessageContent()
            {
                var input = "<23>Jul  4 04:04:04 malformedhost this-message-has-no-colon-or-tag";
                var result = _testedClass.Parse(input);

                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Mail, result.Facility);      // 23 >> 3 = 2
                Assert.Equal(SyslogSeverity.Debug, result.Severity);     // 23 & 7 = 7
                Assert.Equal("malformedhost", result.Hostname);
                Assert.Equal("this-message-has-no-colon-or-tag", result.Message);
            }

            /// <summary>
            /// Verifies the branch where textSpan length is greater than 4 but the first character is not '<'.
            /// Covers: textSpan[0] != '<'
            /// </summary>
            [Fact]
            public void WhenLengthIsValidButFirstCharIsNotBracketThenReturnNull()
            {
                // Arrange - Length is 5, but starts with 'A' instead of '<'
                var input = "A123>";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies the branch where priority is valid, validating the lower boundary condition.
            /// Covers the true block of: pri >= 0 && pri <= 191
            /// </summary>
            [Fact]
            public void WhenPriorityIsExactlyZeroThenProcessAsValidPriority()
            {
                // Arrange - PRI 0 is the lowest bound possible (Kernel + Emergency)
                var input = "<0>Oct 11 22:14:15 host app: message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Kernel, result.Facility);
                Assert.Equal(SyslogSeverity.Emergency, result.Severity);
            }

            /// <summary>
            /// Verifies the branch where priority is negative, validating the lower false boundary condition.
            /// Covers the false block of: pri >= 0
            /// </summary>
            [Fact]
            public void WhenPriorityIsNegativeThenMapToUnknownEnums()
            {
                // Arrange - Negative numbers will fail the validation boundary
                var input = "<-5>Oct 11 22:14:15 host app: message";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(SyslogFacility.Unknown, result.Facility);
                Assert.Equal(SyslogSeverity.Unknown, result.Severity);
            }

            /// <summary>
            /// Verifies the short-circuit return when no space is found after the fixed-length timestamp segment.
            /// Covers: if (spaceIdx == -1) return message;
            /// </summary>
            [Fact]
            public void WhenNoSpaceExistsAfterTimestampThenReturnEarlyWithPropertiesNull()
            {
                // Arrange - "Oct 11 22:14:15 my machine" causes spaceIdx to be -1 after Slice(16)
                var input = "<13>Oct 11 22:14:15 mymachine";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(10, result.Timestamp.Month);
                Assert.Equal("", result.Hostname);
                Assert.Equal("", result.AppName);
                Assert.Equal("", result.Message);
            }

            /// <summary>
            /// Verifies the branch where a colon exists but is the absolute final character of the string.
            /// Covers the false condition of: colonIdx + 1 < span.Length
            /// </summary>
            [Fact]
            public void WhenColonIsTheAbsoluteLastCharacterInStreamThenExtractCleanlyWithoutBorders()
            {
                // Arrange - The line ends exactly on the ':' character
                var input = "<13>Oct 11 22:14:15 host app:";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal("", result.Message); // No index out of bounds, resolves to empty string safely
            }

            /// <summary>
            /// Verifies that an input starting with '<' but having a total length shorter than 4 characters
            /// forces the internal textSpan length check to return null.
            /// </summary>
            [Theory]
            [InlineData("<")]
            [InlineData("<A")]
            [InlineData("<AB")]
            public void WhenInputStartsWithBracketButIsTooShortThenReturnNullFromInternalLengthCheck(string input)
            {
                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }
        }
    }
}