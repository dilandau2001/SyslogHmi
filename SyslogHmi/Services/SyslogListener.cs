using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyslogHmi.Models;

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
        /// Gracefully stops the background network operations and disconnects any active clients.
        /// </summary>
        /// <summary>
        /// Instantly stops the network listeners and signals all background tasks to cancel.
        /// This method does not block the calling thread.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            // 1. Instantly signal all async loops (ReadAsync, ReceiveAsync) to cancel
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
        /// Asynchronously reads incoming network text streams from an established TCP client connection.
        /// </summary>
        /// <param name="client">The active TCP connection instance.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[4096];
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0)
                            break; // Client closed connection cleanly

                        string rawMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
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
        /// Parses raw network strings according to standard BSD/IETF Syslog layouts (<see cref="System.Net.Sockets"/> RFC 3164 / RFC 5424 specifications).
        /// </summary>
        /// <param name="rawMessage">The unmodified text string received directly from the socket network packet buffer.</param>
        /// <returns>A structured model representation of the message parameters, or null if schema initialization fails.</returns>
        private SyslogMessage ParseSyslogMessage(string rawMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rawMessage))
                    return null;

                var message = new SyslogMessage
                {
                    FullMessage = rawMessage,
                    ReceivedTime = DateTime.UtcNow,
                    Timestamp = DateTime.UtcNow // Mantener como fallback por seguridad
                };

                // 1. Validar el inicio y extraer la prioridad <PRI>
                int priEnd = rawMessage.IndexOf('>');
                if (priEnd == -1 || !rawMessage.StartsWith("<"))
                    return null;

                string priStr = rawMessage.Substring(1, priEnd - 1);
                if (!int.TryParse(priStr, out int pri))
                    return null;

                message.Facility = (pri >> 3) & 0x1F;
                message.Severity = pri & 0x07;
                message.FacilityName = GetFacilityName(message.Facility);
                message.SeverityName = GetSeverityName(message.Severity);

                string rest = rawMessage.Substring(priEnd + 1).Trim();
                var parts = rest.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return message;

                // ==========================================
                // NUEVO: PARSEO DEL TIMESTAMP DEL SIMULADOR (RFC 5424)
                // ==========================================
                // El primer elemento tras el '>' en el simulador es el timestamp (ej: 2026-05-21T00:26:05.123Z)
                string timestampStr = parts[0];
                int nextPartIndex = 1;

                // Intentamos parsear el formato ISO con el que el simulador envía el texto
                if (DateTime.TryParse(timestampStr, out DateTime parsedDate))
                {
                    // Forzamos explícitamente el Kind a UTC porque sabemos que tu simulador añade la 'Z'
                    message.Timestamp = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }
                else
                {
                    // Si por algún motivo falla (o es formato RFC 3164 antiguo), mantenemos el fallback actual
                    message.Timestamp = DateTime.UtcNow;
                    nextPartIndex = 0; // No avanzamos el índice si el primer elemento no era una fecha
                }

                // Ajustamos la lectura del Hostname basándonos en si consumimos el token de la fecha o no
                if (parts.Length > nextPartIndex)
                {
                    message.Hostname = parts[nextPartIndex];
                    nextPartIndex++;
                }

                // Procesar la aplicación y el PID desplazando los índices correspondientes
                if (parts.Length > nextPartIndex)
                {
                    var appPart = parts[nextPartIndex];
                    nextPartIndex++;

                    int bracketPos = appPart.IndexOf('[');
                    if (bracketPos != -1)
                    {
                        message.AppName = appPart.Substring(0, bracketPos);
                        int pidEnd = appPart.IndexOf(']', bracketPos);
                        if (pidEnd != -1 && int.TryParse(appPart.Substring(bracketPos + 1, pidEnd - bracketPos - 1), out int pid))
                        {
                            message.ProcessId = pid;
                        }
                    }
                    else
                    {
                        message.AppName = appPart.TrimEnd(':');
                    }
                }

                // Aislar el mensaje final
                int messageStart = rawMessage.IndexOf(": ", priEnd + 1, StringComparison.Ordinal);
                if (messageStart != -1)
                {
                    message.Message = rawMessage[(messageStart + 2)..];
                }
                else if (parts.Length > nextPartIndex)
                {
                    message.Message = string.Join(" ", parts.Skip(nextPartIndex));
                }
                else
                {
                    message.Message = rest;
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
