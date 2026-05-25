using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SyslogHmi.Services
{
    public class SyslogMessageSender
    {
        private readonly Random _random = new Random();

        private static readonly string[] Hostnames = { "web-server-01", "db-server-01", "app-server-02", "cache-01", "router-01", "firewall-01" };
        private static readonly string[] Applications = { "nginx", "postgresql", "java-app", "redis", "kernel", "sshd", "systemd", "cron", "apache2" };
        private static readonly int[] Severities = { 0, 1, 2, 3, 4, 5, 6, 7 };
        private static readonly int[] Facilities = { 0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23 };

        private static readonly string[] Messages = new[]
        {
            "User 'galias' successfully authenticated from IP 192.168.1.45.\nSession token generated.\nRetrying",
            "Started User Manager",
            "Database connection established",
            "Failed login attempt from 192.168.1.100",
            "Memory usage exceeded threshold",
            "Backup completed successfully",
            "Configuration file reloaded",
            "Network interface down",
            "Certificate will expire in 30 days",
            "Disk usage at 85%",
            "Service restarted",
            "Authentication failed for user admin",
            "Request timeout occurred",
            "Cache invalidated",
            "SSL handshake failed",
            "Queue depth increasing"
        };

        public void SendRandomSyslogMessage(string host = "localhost", int port = 514)
        {
            try
            {
                var hostname = Hostnames[_random.Next(Hostnames.Length)];
                var app = Applications[_random.Next(Applications.Length)];
                var severity = Severities[_random.Next(Severities.Length)];
                var facility = Facilities[_random.Next(Facilities.Length)];
                var message = Messages[_random.Next(Messages.Length)];
                var pid = _random.Next(1000, 65535);

                // RFC 5424 format (simplified)
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var priority = (facility * 8) + severity;

                var syslogMessage = $"<{priority}>{timestamp} {hostname} {app}[{pid}]: {message}";

                SendUdpMessage(syslogMessage, host, port);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending syslog message: {ex.Message}");
            }
        }

        public async Task SendRandomSyslogMessageAsync(string host = "localhost", int port = 514)
        {
            await Task.Run(() => SendRandomSyslogMessage(host, port));
        }

        private void SendUdpMessage(string message, string host, int port)
        {
            using (var udpClient = new UdpClient())
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                udpClient.Send(bytes, bytes.Length, host, port);
            }
        }
    }
}
