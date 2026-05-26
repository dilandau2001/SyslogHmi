using SyslogHmi.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Provides a dual-protocol network listener capable of receiving and parsing 
    /// standard Syslog messages over both TCP and UDP concurrently.
    /// </summary>
    public sealed class SyslogListener : IDisposable
    {
        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentBag<Task> _activeClientTasks = new ConcurrentBag<Task>();
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Occurs when a new Syslog message is successfully received and parsed.
        /// </summary>
        public event Action<SyslogMessage> MessageReceived;

        /// <summary>
        /// Starts listening for incoming Syslog messages on the specified TCP and UDP ports.
        /// </summary>
        /// <param name="tcpPort">The local port to listen for TCP connections.</param>
        /// <param name="udpPort">The local port to listen for UDP datagrams.</param>
        /// <exception cref="InvalidOperationException">Thrown if the listener service is already running.</exception>
        public void Start(int tcpPort, int udpPort)
        {
            if (_isRunning)
                throw new InvalidOperationException("Listener is already running");

            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, tcpPort);
                _tcpListener.Start();
                _ = AcceptTcpConnectionsAsync(_cancellationTokenSource.Token);

                _udpClient = new UdpClient(udpPort);
                _ = ReceiveUdpMessagesAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                Stop(); // Symmetrical, instant, and safe.
                throw new InvalidOperationException($"Failed to start syslog listener: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Instantly stops the network listeners and signals all background tasks to cancel.
        /// This method does not block the calling thread.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            // 1. Instantly signal all async loops (ReadLineAsync, ReceiveAsync) to cancel
            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException) { }

            // 2. Tear down the sockets. This forces blocked network streams to fault and exit immediately
            try
            {
                _tcpListener?.Stop();
                _tcpListener = null;

                _udpClient?.Close();
                _udpClient = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing sockets: {ex.Message}");
            }

            // 3. Clean up the cancellation source token allocations
            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch (ObjectDisposedException) { }
            _cancellationTokenSource = null;

            // 4. Flush the concurrent bag reference tracking so completed tasks can be garbage collected
            while (_activeClientTasks.TryTake(out _)) { }
        }

        /// <summary>
        /// Asynchronously listens for incoming TCP client connection requests.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        private async Task AcceptTcpConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);

                    // Track individual client connection lifetimes to prevent detached leaking tasks
                    var clientTask = HandleTcpClientAsync(client, cancellationToken);
                    _activeClientTasks.Add(clientTask);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TCP Listener connection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronously reads incoming network text streams line-by-line from an established TCP connection.
        /// Implements Non-Transparent Framing standard.
        /// </summary>
        /// <param name="client">The active TCP connection instance.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (var stream = client.GetStream())
            // StreamReader naturally abstracts buffer fragmentations and line aggregations (\n or \r\n)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string rawMessage = await reader.ReadLineAsync(cancellationToken);
                        if (rawMessage == null)
                            break; // Client closed connection cleanly

                        var message = ParseSyslogMessage(rawMessage);
                        if (message != null)
                        {
                            MessageReceived?.Invoke(message);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TCP Client pipeline error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Asynchronously polls the UDP socket to process incoming datagram payloads.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        private async Task ReceiveUdpMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    string rawMessage = Encoding.UTF8.GetString(result.Buffer);

                    var message = ParseSyslogMessage(rawMessage);
                    if (message != null)
                    {
                        MessageReceived?.Invoke(message);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UDP Listener payload error: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses raw network strings according to standard BSD (RFC 3164) or IETF (RFC 5424) Syslog layouts.
        /// </summary>
        /// <param name="rawMessage">The unmodified text string received directly from the socket network packet buffer.</param>
        /// <returns>A structured model representation of the message parameters, or null if validation fails.</returns>
        private SyslogMessage ParseSyslogMessage(string rawMessage)
        {
            if (string.IsNullOrWhiteSpace(rawMessage))
                return null;

            try
            {
                // Utilize Spans to execute zero-allocation structural checks on the heap
                ReadOnlySpan<char> textSpan = rawMessage.AsSpan().Trim();

                if (textSpan.Length < 4 || textSpan[0] != '<')
                    return null;

                int priEnd = textSpan.IndexOf('>');
                if (priEnd == -1)
                    return null;

                // 1. Extract and calculate Priority, Facility, and Severity properties
                var priSpan = textSpan.Slice(1, priEnd - 1);
                if (!int.TryParse(priSpan, out int pri))
                    return null;

                var message = new SyslogMessage
                {
                    FullMessage = rawMessage,
                    ReceivedTime = DateTime.UtcNow,
                    Facility = (pri >> 3) & 0x1F,
                    Severity = pri & 0x07
                };
                message.FacilityName = GetFacilityName(message.Facility);
                message.SeverityName = GetSeverityName(message.Severity);

                // Advance cursor right past the closing '>' bracket
                ReadOnlySpan<char> rest = textSpan.Slice(priEnd + 1);
                if (rest.IsEmpty)
                    return message;

                // 2. Identify RFC protocol layout by evaluating version digit presence (RFC 5424 starts with version, e.g., '1 ')
                bool isRfc5424 = false;
                if (char.IsDigit(rest[0]))
                {
                    int spaceIdx = rest.IndexOf(' ');
                    if (spaceIdx != -1)
                    {
                        var versionSpan = rest.Slice(0, spaceIdx);
                        if (int.TryParse(versionSpan, out int version) && version == 1)
                        {
                            isRfc5424 = true;
                            rest = rest.Slice(spaceIdx + 1); // Advance past version block directly to Timestamp
                        }
                    }
                }

                // 3. Forward tracking references to specialized internal engines
                if (isRfc5424)
                {
                    ParseRfc5424(rest, message);
                }
                else
                {
                    ParseRfc3164(rest, message);
                }

                return message;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Syslog layout structural parsing exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Detailed extraction routine targeted for the strict ISO-8601 structural layouts of IETF RFC 5424.
        /// </summary>
        private void ParseRfc5424(ReadOnlySpan<char> span, SyslogMessage message)
        {
            // Expected layout template: 2026-05-21T00:26:05.123Z hostname appname [PID] MSGID STRUCTURED-DATA msg...

            // 1. TIMESTAMP
            int space1 = span.IndexOf(' ');
            if (space1 == -1) return;
            var timeSpan = span.Slice(0, space1);
            if (DateTime.TryParse(timeSpan, out DateTime parsedDate))
            {
                message.Timestamp = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }
            span = span.Slice(space1 + 1);

            // 2. HOSTNAME
            int space2 = span.IndexOf(' ');
            if (space2 == -1) return;
            message.Hostname = span.Slice(0, space2).ToString();
            span = span.Slice(space2 + 1);

            // 3. APP-NAME
            int space3 = span.IndexOf(' ');
            if (space3 == -1) return;
            message.AppName = span.Slice(0, space3).ToString();
            span = span.Slice(space3 + 1);

            // 4. PROCID (Process ID)
            int space4 = span.IndexOf(' ');
            if (space4 == -1) return;
            var procIdSpan = span.Slice(0, space4);
            if (int.TryParse(procIdSpan, out int pid))
            {
                message.ProcessId = pid;
            }
            span = span.Slice(space4 + 1);

            // 5. MSGID (New: Advance past MSGID field)
            int space5 = span.IndexOf(' ');
            if (space5 == -1) return;
            // We don't map MSGID to a property here, but we advance the span past it
            span = span.Slice(space5 + 1);

            // 6. STRUCTURED-DATA (Handled cleanly whether it's a null '-' or a bracketed block '[...]')
            if (span.Length > 0 && span[0] == '-')
            {
                int space6 = span.IndexOf(' ');
                if (space6 != -1)
                {
                    span = span.Slice(space6 + 1); // Skip the '-' and the trailing space
                }
            }
            else if (span.Length > 0 && span[0] == '[')
            {
                int endBracket = span.IndexOf(']');
                if (endBracket != -1 && endBracket < span.Length - 1)
                {
                    span = span.Slice(endBracket + 2); // Skip the structured chunk and its trailing space
                }
            }

            // 7. RAW MESSAGE CONTENT
            message.Message = span.ToString();
        }

        /// <summary>
        /// Detailed positional parsing engine designed to isolate properties inside legacy BSD RFC 3164 templates.
        /// </summary>
        private void ParseRfc3164(ReadOnlySpan<char> span, SyslogMessage message)
        {
            // Expected layout template: Oct 11 22:14:15 hostname app[123]: message content
            if (span.Length < 16)
            {
                message.Timestamp = DateTime.UtcNow;
                message.Message = span.ToString();
                return;
            }

            // 1. TIMESTAMP (The introductory 15 characters are fixed positions under BSD standards)
            var timeSpan = span.Slice(0, 15);
            if (DateTime.TryParse(timeSpan, out DateTime parsedDate))
            {
                // Note: Legacy RFC 3164 completely drops the execution year context.
                // We inject the running OS year parameter to safeguard grid timelines from drifting.
                message.Timestamp = new DateTime(DateTime.UtcNow.Year, parsedDate.Month, parsedDate.Day,
                                                 parsedDate.Hour, parsedDate.Minute, parsedDate.Second, DateTimeKind.Utc);
            }
            else
            {
                message.Timestamp = DateTime.UtcNow;
            }

            // Advance past the fixed timestamp layout blocks and their trailing space boundary
            span = span.Slice(16);

            // 2. HOSTNAME
            int spaceIdx = span.IndexOf(' ');
            if (spaceIdx == -1) return;
            message.Hostname = span.Slice(0, spaceIdx).ToString();
            span = span.Slice(spaceIdx + 1);

            // 3. TAG / APPNAME (Typically formatted ending in a colon ':' or inside a tracked bracket '[PID]:')
            int colonIdx = span.IndexOf(':');
            if (colonIdx != -1)
            {
                var tagSpan = span.Slice(0, colonIdx);
                int bracketOpen = tagSpan.IndexOf('[');
                int bracketClose = tagSpan.IndexOf(']');

                if (bracketOpen != -1 && bracketClose != -1)
                {
                    message.AppName = tagSpan.Slice(0, bracketOpen).ToString();
                    var pidSpan = tagSpan.Slice(bracketOpen + 1, bracketClose - bracketOpen - 1);
                    if (int.TryParse(pidSpan, out int pid))
                    {
                        message.ProcessId = pid;
                    }
                }
                else
                {
                    message.AppName = tagSpan.ToString();
                }

                // The message content resides right after the structural signature pattern ": "
                if (colonIdx + 1 < span.Length && span[colonIdx + 1] == ' ')
                    message.Message = span.Slice(colonIdx + 2).ToString();
                else
                    message.Message = span.Slice(colonIdx + 1).ToString();
            }
            else
            {
                // Fallback implementation logic if the tag does not match standard BSD patterns
                message.Message = span.ToString();
            }
        }

        /// <summary>
        /// Maps standard numerical severity levels to descriptive human-readable strings.
        /// </summary>
        private string GetSeverityName(int severity) => severity switch
        {
            0 => "Emergency",
            1 => "Alert",
            2 => "Critical",
            3 => "Error",
            4 => "Warning",
            5 => "Notice",
            6 => "Info",
            7 => "Debug",
            _ => "Unknown"
        };

        /// <summary>
        /// Maps numerical facility classification categories to descriptive structural strings.
        /// </summary>
        private string GetFacilityName(int facility) => facility switch
        {
            0 => "Kernel",
            1 => "User",
            2 => "Mail",
            3 => "Daemon",
            4 => "Auth",
            5 => "Syslog",
            6 => "LPR",
            7 => "News",
            8 => "UUCP",
            9 => "Cron",
            10 => "AuthPriv",
            11 => "FTP",
            12 => "NTP",
            13 => "LogAudit",
            14 => "LogAlert",
            15 => "ClockDaemon",
            16 => "Local0",
            17 => "Local1",
            18 => "Local2",
            19 => "Local3",
            20 => "Local4",
            21 => "Local5",
            22 => "Local6",
            23 => "Local7",
            _ => "Unknown"
        };

        /// <summary>
        /// Releases all managed and unmanaged network resources used by the <see cref="SyslogListener"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes object allocations during garbage collection routines or explicit disposals.
        /// </summary>
        /// <param name="disposing">True if executing via explicit object disposal calls.</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                }
                _disposed = true;
            }
        }
    }
}