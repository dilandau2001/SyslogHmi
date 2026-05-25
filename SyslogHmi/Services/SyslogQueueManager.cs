using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyslogHmi.Models;

namespace SyslogHmi.Services
{
    public class SyslogQueueManager
    {
        private readonly DatabaseService _databaseService;

        // Dos colas independientes en memoria (Hilos de ejecución paralelos)
        private readonly ConcurrentQueue<SyslogMessage> _databaseQueue = new();
        private readonly ConcurrentQueue<SyslogMessage> _uiQueue = new();

        private CancellationTokenSource _cts;
        private Task _databaseWriterTask;
        private bool _isRunning;

        public SyslogQueueManager(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            // Arrancamos el hilo dedicado exclusivamente a escribir en la base de datos SQLite
            _databaseWriterTask = Task.Run(() => ProcessDatabaseQueueAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts?.Cancel();
            try
            {
                _databaseWriterTask?.Wait(2000); // Espera un máximo de 2 segundos a que termine el lote actual
            }
            catch { }
            _cts?.Dispose();
        }

        /// <summary>
        /// Punto de entrada único desde el SyslogListener. Reparte el mensaje a ambas colas en microsegundos.
        /// </summary>
        public void EnqueueMessage(SyslogMessage message)
        {
            if (message == null) return;
            _databaseQueue.Enqueue(message);
            _uiQueue.Enqueue(message);
        }

        /// <summary>
        /// Método que consumirá el MainViewModel para extraer lo acumulado para la pantalla.
        /// </summary>
        public List<SyslogMessage> DequeueUiBatch()
        {
            var batch = new List<SyslogMessage>();
            // Vaciamos todo lo que tenga la cola de UI en este instante
            while (_uiQueue.TryDequeue(out var message))
            {
                batch.Add(message);
            }
            return batch;
        }

        private async Task ProcessDatabaseQueueAsync(CancellationToken token)
        {
            var localBatch = new List<SyslogMessage>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1. Extraemos TODO lo que se haya acumulado en la cola de la DB hasta ahora
                    while (_databaseQueue.TryDequeue(out var message))
                    {
                        localBatch.Add(message);

                        // Ponemos un tope por lote para no saturar la memoria RAM en picos masivos
                        if (localBatch.Count >= 10000) break;
                    }

                    // 2. 🔥 AQUÍ ESTÁ LA CORRECCIÓN CRUCIAL:
                    // Si hay mensajes en el lote local, los guardamos físicamente en SQLite
                    if (localBatch.Count > 0)
                    {
                        _databaseService.SaveMessagesBulk(localBatch);
                        localBatch.Clear(); // Vaciamos el lote local para la siguiente vuelta
                    }

                    // 3. Pequeño respiro de 100ms si la cola se vacía para no ahogar la CPU
                    await Task.Delay(100, token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error crítico guardando en SQLite: {ex.Message}");
                    await Task.Delay(1000, token); // Si falla (p.ej. disco bloqueado), esperamos 1s antes de reintentar
                }
            }
        }

        public void ClearPendingQueues()
        {
            _databaseQueue.Clear();
            _uiQueue.Clear();
        }
    }
}