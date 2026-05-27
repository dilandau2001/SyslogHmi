using SyslogHmi.Services;

namespace SyslogHmiTests.Services
{
    /// <summary>
    /// Tests for the main orchestrator SyslogParser class to achieve 100% coverage.
    /// </summary>
    public class SyslogParserTests
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
                _testedClass = new SyslogParser();
            }

            /// <summary>
            /// Verifies that passing a null, empty, or whitespace string returns null immediately.
            /// Covers: if (string.IsNullOrWhiteSpace(rawMessage))
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
            /// Verifies that if no closing bracket exists, it routes to RFC 3164 as a fallback 
            /// and the inner parser handles the malformed input (returning null).
            /// Covers: if (closingBracketIdx == -1)
            /// </summary>
            [Fact]
            public void WhenMissingClosingBracketThenFallbackToRfc3164AndReturnNull()
            {
                // Arrange
                string input = "Malformed message without brackets";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.Null(result);
            }

            /// <summary>
            /// Verifies that if the closing bracket is the absolute last character, it routes to RFC 3164 
            /// as a fallback and the inner parser handles it.
            /// Covers: closingBracketIdx + 1 >= rawMessage.Length
            /// </summary>
            [Fact]
            public void WhenMessageEndsExactlyAtClosingBracketThenFallbackToRfc3164AndReturnNotNull()
            {
                // Arrange
                string input = "<13>";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
            }

            /// <summary>
            /// Verifies that if a digit follows the closing bracket, it dynamically routes to RFC 5424.
            /// Covers: if (char.IsDigit(lookAheadChar)) -> True block
            /// </summary>
            [Fact]
            public void WhenDigitFollowsClosingBracketThenRouteToRfc5424Parser()
            {
                // Arrange - Valid standard RFC 5424 log starting with version digit '1'
                string input = "<13>1 2026-05-27T22:14:15Z host app 123 ID45 - msg";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("host", result.Hostname);
                Assert.Equal("app", result.AppName);
                Assert.Equal(123, result.ProcessId);
                // Probar que el objeto fue construido por el parser moderno (RFC 5424)
                Assert.Equal("msg", result.Message);
            }

            /// <summary>
            /// Verifies that if a non-digit character (like a letter) follows the closing bracket, 
            /// it dynamically routes to RFC 3164.
            /// Covers: if (char.IsDigit(lookAheadChar)) -> False block (Fallthrough to end)
            /// </summary>
            [Fact]
            public void WhenNonDigitFollowsClosingBracketThenRouteToRfc3164Parser()
            {
                // Arrange - Valid standard RFC 3164 log starting with month letter 'O'
                string input = "<13>Oct 11 22:14:15 mymachine su: 'su root' failed";

                // Act
                var result = _testedClass.Parse(input);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("mymachine", result.Hostname);
                Assert.Equal("su", result.AppName);
                // Probar que el objeto fue construido por el parser antiguo (RFC 3164)
                Assert.Equal("'su root' failed", result.Message);
            }
        }
    }
}