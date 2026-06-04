using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyslogHmi.Models;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Manages independent, asynchronous memory queues for processing incoming Syslog messages.
    /// Implements a Producer-Consumer pattern splitting work between the UI display and database persistence.
    /// </summary>
    public class SyslogQueueManager
    {
        private readonly DatabaseService _databaseService;

        // Two independent in-memory queues (Supporting parallel processing execution threads)
        private readonly ConcurrentQueue<SyslogMessage> _databaseQueue = new();
        private readonly ConcurrentQueue<SyslogMessage> _uiQueue = new();

        private CancellationTokenSource _cts;
        private Task _databaseWriterTask;
        private bool _isRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyslogQueueManager"/> class.
        /// </summary>
        /// <param databaseService="databaseService">The database persistence service instance.</param>
        public SyslogQueueManager(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        /// <summary>
        /// Starts the background consumer worker thread for database persistence.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            // Fire and forget the dedicated thread exclusively responsible for writing to the SQLite database
            _databaseWriterTask = Task.Run(() => ProcessDatabaseQueueAsync(_cts.Token));
        }

        /// <summary>
        /// Gracefully stops the background database worker thread, allowing a brief window to flush remaining data.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts?.Cancel();

            try
            {
                // Wait for a maximum of 2 seconds for the current bulk batch to finish processing
                _databaseWriterTask?.Wait(2000);
            }
            catch (Exception)
            {
                // Prevent teardown exceptions from crashing the application during shutdown
            }
            finally
            {
                _cts?.Dispose();
            }
        }

        /// <summary>
        /// Single entry point called by the SyslogListener. 
        /// Dispatches the message to both queues simultaneously in microseconds.
        /// </summary>
        /// <param name="message">The received Syslog message payload.</param>
        public void EnqueueMessage(SyslogMessage message)
        {
            if (message == null) return;

            _databaseQueue.Enqueue(message);
            _uiQueue.Enqueue(message);
        }

        /// <summary>
        /// Consumer method called periodically by the MainViewModel to extract accumulated logs for screen rendering.
        /// </summary>
        /// <returns>A list containing all accumulated messages since the last poll.</returns>
        public List<SyslogMessage> DequeueUiBatch()
        {
            var batch = new List<SyslogMessage>();

            // Drain everything currently sitting inside the UI thread-safe queue at this exact instant
            while (_uiQueue.TryDequeue(out var message))
            {
                batch.Add(message);
            }
            return batch;
        }

        /// <summary>
        /// Continuous background worker loop that groups messages and writes them to the database in bulk.
        /// </summary>
        private async Task ProcessDatabaseQueueAsync(CancellationToken token)
        {
            var localBatch = new List<SyslogMessage>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1. Extract EVERYTHING accumulated in the database queue up to this moment
                    while (_databaseQueue.TryDequeue(out var message))
                    {
                        localBatch.Add(message);

                        // Cap the local batch size to prevent RAM saturation during massive traffic spikes
                        if (localBatch.Count >= 10000) break;
                    }

                    // 2. CRUCIAL OPTIMIZATION:
                    // If the local batch has entries, save them physically to SQLite using a single bulk transaction
                    if (localBatch.Count > 0)
                    {
                        _databaseService.SaveMessagesBulk(localBatch);
                        localBatch.Clear(); // Clear the local list buffer to prepare for the next loop round
                    }

                    // 3. Take a short 100ms breather if the queue is empty to avoid choking CPU resources (100% Core utilization)
                    await Task.Delay(100, token);
                }
                catch (OperationCanceledException)
                {
                    // Expected exception catch when cancellation token triggers; gracefully exit loop
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Critical error saving to SQLite: {ex.Message}");

                    // If a failure occurs (e.g., locked database file or full disk), wait 1 second before retrying
                    await Task.Delay(1000, token);
                }
            }
        }

        /// <summary>
        /// Instantly purges all unread contents residing in both memory queues.
        /// </summary>
        public void ClearPendingQueues()
        {
            _databaseQueue.Clear();
            _uiQueue.Clear();
        }
    }
}