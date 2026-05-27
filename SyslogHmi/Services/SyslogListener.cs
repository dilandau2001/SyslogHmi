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
    /// Provides a dual-protocol network listener focused strictly on receiving network text 
    /// streams over TCP and UDP, delegating message validation and parsing to an external engine.
    /// </summary>
    public sealed class SyslogListener : IDisposable
    {
        private readonly ISyslogParser _parser;
        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentBag<Task> _activeClientTasks = [];
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Occurs when a new Syslog message is successfully received and parsed.
        /// </summary>
        public event Action<SyslogMessage> MessageReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyslogListener"/> class.
        /// </summary>
        /// <param name="parser">The polymorphic parsing engine strategy implementation.</param>
        public SyslogListener(ISyslogParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyslogListener"/> class using the default composite router.
        /// </summary>
        public SyslogListener() : this(new SyslogParser())
        {
        }

        /// <summary>
        /// Starts listening for incoming Syslog streams on the specified ports.
        /// </summary>
        public void Start(int tcpPort, int udpPort)
        {
            if (_isRunning)
                throw new InvalidOperationException("Listener is already running");

            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            try
            {
                // Initialize and fire up the TCP socket listener pipeline
                _tcpListener = new TcpListener(IPAddress.Any, tcpPort);
                _tcpListener.Start();
                _ = AcceptTcpConnectionsAsync(_cancellationTokenSource.Token);

                // Initialize and fire up the UDP datagram socket listener pipeline
                _udpClient = new UdpClient(udpPort);
                _ = ReceiveUdpMessagesAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                Stop();
                throw new InvalidOperationException($"Failed to start syslog listener: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Instantly tears down network resources and requests cancellation across background stream runners.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            try { _cancellationTokenSource?.Cancel(); } catch (ObjectDisposedException) { }

            try
            {
                _tcpListener?.Stop();
                _tcpListener = null;

                _udpClient?.Close();
                _udpClient = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing network sockets: {ex.Message}");
            }

            try { _cancellationTokenSource?.Dispose(); } catch (ObjectDisposedException) { }
            _cancellationTokenSource = null;

            while (_activeClientTasks.TryTake(out _)) { }
        }

        private async Task AcceptTcpConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                    var clientTask = HandleTcpClientAsync(client, cancellationToken);
                    _activeClientTasks.Add(clientTask);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TCP Listener connection fault: {ex.Message}");
            }
        }

        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var rawMessage = await reader.ReadLineAsync(cancellationToken);
                        if (rawMessage == null)
                            break; // Connection ended clean by remote client

                        // Delegate raw textual parsing logic completely to our injected dependency
                        var message = _parser.Parse(rawMessage);
                        if (message != null)
                        {
                            MessageReceived?.Invoke(message);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TCP Client pipeline fault: {ex.Message}");
                }
            }
        }

        private async Task ReceiveUdpMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    var rawMessage = Encoding.UTF8.GetString(result.Buffer);

                    // Delegate raw textual parsing logic completely to our injected dependency
                    var message = _parser.Parse(rawMessage);
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
                System.Diagnostics.Debug.WriteLine($"UDP Listener payload fault: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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