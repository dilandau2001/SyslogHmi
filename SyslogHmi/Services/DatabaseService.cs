using Microsoft.Data.Sqlite;
using SyslogHmi.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SyslogHmi.Services
{
    /// <summary>
    /// Sealed service class providing SQLite-based persistence for syslog messages and color rules.
    /// Manages database initialization, message storage/retrieval, color rule management, and connection lifecycle.
    /// Supports both synchronous and asynchronous disposal.
    /// </summary>
    public sealed class DatabaseService : IDisposable, IAsyncDisposable, IDatabaseService
    {
        /// <summary>
        /// The file path where the SQLite database is stored.
        /// Located in the application data directory by default.
        /// </summary>
        private readonly string _databasePath;

        /// <summary>
        /// The SQLite database connection maintained throughout the service lifetime.
        /// </summary>
        private readonly SqliteConnection _connection;

        /// <summary>
        /// Initializes a new instance of the DatabaseService class.
        /// Creates the database file if it doesn't exist and initializes the required schema.
        /// Sets up SQLite connection with optimized performance settings (WAL mode, NORMAL synchronous).
        /// </summary>
        /// <param name="databaseName">The name of the database file. Default is "syslog_messages.db".</param>
        public DatabaseService(string databaseName = "syslog_messages.db")
        {
            // Construct the full database path in the application data directory
            _databasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SyslogHmi",
                databaseName);

            // Ensure the directory exists before creating the connection
            EnsureDirectoryExists();

            // Create and open the SQLite connection
            var connectionString = $"Data Source={_databasePath};";
            _connection = new SqliteConnection(connectionString);
            InitializeDatabase();
        }

        /// <summary>
        /// Ensures that the directory for the database file exists.
        /// Creates the directory if it doesn't exist to prevent file creation errors.
        /// </summary>
        private void EnsureDirectoryExists()
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Initializes the SQLite database with optimized performance settings and required schema.
        /// Opens the connection, enables Write-Ahead Logging (WAL) and NORMAL synchronous mode for improved performance.
        /// Creates three tables if they don't exist: SyslogMessages, ColorRules, and ColorRuleConditions.
        /// </summary>
        /// <remarks>
        /// PRAGMA settings explained:
        /// - journal_mode=WAL: Enables Write-Ahead Logging for better concurrency and performance
        /// - synchronous=NORMAL: Reduces sync overhead while maintaining data integrity
        /// ColorRuleConditions.RuleId is defined as a foreign key referencing ColorRules.Id.
        /// </remarks>
        private void InitializeDatabase()
        {   
            _connection.Open();

            // Enable high-performance SQLite settings
            using (var walCommand = _connection.CreateCommand())
            {
                walCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                walCommand.ExecuteNonQuery();
            }

            // Create the schema (tables) if they don't exist
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = 
                """
                    CREATE TABLE IF NOT EXISTS SyslogMessages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Hostname TEXT,
                    AppName TEXT,
                    ProcessId INTEGER,
                    MessageId TEXT,
                    Severity INTEGER,
                    Facility INTEGER,
                    SeverityName TEXT,
                    FacilityName TEXT,
                    Message TEXT,
                    FullMessage TEXT,
                    ReceivedTime TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS ColorRules (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    MatchType TEXT NOT NULL,
                    MatchValue TEXT,
                    BackgroundColor TEXT,
                    ForegroundColor TEXT,
                    IsActive INTEGER DEFAULT 1,
                    Priority INTEGER DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS ColorRuleConditions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RuleId INTEGER NOT NULL,
                    PropertyName TEXT NOT NULL,
                    ComparisonType INTEGER NOT NULL,
                    ComparisonValue TEXT,
                    CaseSensitive INTEGER DEFAULT 0,
                    AlternativeValues TEXT,
                    FOREIGN KEY(RuleId) REFERENCES ColorRules(Id)
                    );
                """;

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Saves a single syslog message to the database.
        /// Uses parameterized queries to prevent SQL injection.
        /// </summary>
        /// <param name="message">The syslog message to save.</param>
        private void SaveMessage(SyslogMessage message)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                                                      INSERT INTO SyslogMessages 
                                                      (Timestamp, Hostname, AppName, ProcessId, MessageId, Severity, Facility, 
                                                       SeverityName, FacilityName, Message, FullMessage, ReceivedTime)
                                                      VALUES (@timestamp, @hostname, @appName, @processId, @messageId, 
                                                              @severity, @facility, @severityName, @facilityName, 
                                                              @message, @fullMessage, @receivedTime)

                                  """;

            // Add parameters with message field values (null-safe)
            command.Parameters.AddWithValue("@timestamp", message.Timestamp.ToString("o"));
            command.Parameters.AddWithValue("@hostname", message.Hostname ?? "");
            command.Parameters.AddWithValue("@appName", message.AppName ?? "");
            command.Parameters.AddWithValue("@processId", message.ProcessId);
            command.Parameters.AddWithValue("@messageId", message.MessageId ?? "");
            command.Parameters.AddWithValue("@severity", message.Severity);
            command.Parameters.AddWithValue("@facility", message.Facility);
            command.Parameters.AddWithValue("@severityName", message.SeverityName ?? "");
            command.Parameters.AddWithValue("@facilityName", message.FacilityName ?? "");
            command.Parameters.AddWithValue("@message", message.Message ?? "");
            command.Parameters.AddWithValue("@fullMessage", message.FullMessage ?? "");
            command.Parameters.AddWithValue("@receivedTime", message.ReceivedTime.ToString("o"));

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Saves a list of syslog messages to the database within a transaction.
        /// Ensures atomic operation: all messages are saved or none if an error occurs.
        /// </summary>
        /// <param name="messages">The list of messages to save.</param>
        public void SaveMessages(List<SyslogMessage> messages)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                // Save each message individually within the transaction
                foreach (var message in messages)
                {
                    SaveMessage(message);
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Retrieves all syslog messages from the database, ordered by most recent first.
        /// Results are limited to prevent loading excessive data into memory.
        /// </summary>
        /// <param name="limit">Maximum number of messages to retrieve (default: 10000).</param>
        /// <returns>A list of syslog messages ordered by received time (descending).</returns>
        public List<SyslogMessage> GetAllMessages(int limit = 10000)
        {
            var messages = new List<SyslogMessage>();

            using var command = _connection.CreateCommand();
            command.CommandText = $"""
                                                       SELECT Id, Timestamp, Hostname, AppName, ProcessId, MessageId, 
                                                              Severity, Facility, SeverityName, FacilityName, Message, FullMessage, ReceivedTime
                                                       FROM SyslogMessages
                                                       ORDER BY ReceivedTime DESC
                                                       LIMIT {limit}

                                   """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(MapReaderToMessage(reader));
            }

            return messages;
        }

        /// <summary>
        /// Retrieves syslog messages filtered by severity level.
        /// Returns messages ordered by most recent first.
        /// </summary>
        /// <param name="severity">The severity level to filter by.</param>
        /// <param name="limit">Maximum number of messages to retrieve (default: 10000).</param>
        /// <returns>A list of messages matching the specified severity.</returns>
        public List<SyslogMessage> GetMessagesBySeverity(int severity, int limit = 10000)
        {
            var messages = new List<SyslogMessage>();

            using var command = _connection.CreateCommand();
            command.CommandText = $"""
                                                       SELECT Id, Timestamp, Hostname, AppName, ProcessId, MessageId, 
                                                              Severity, Facility, SeverityName, FacilityName, Message, FullMessage, ReceivedTime
                                                       FROM SyslogMessages
                                                       WHERE Severity = @severity
                                                       ORDER BY ReceivedTime DESC
                                                       LIMIT {limit}

                                   """;
            command.Parameters.AddWithValue("@severity", severity);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(MapReaderToMessage(reader));
            }

            return messages;
        }

        /// <summary>
        /// Retrieves syslog messages containing a specific keyword.
        /// Searches both the short message and full message fields (case-insensitive substring match).
        /// </summary>
        /// <param name="keyword">The keyword to search for.</param>
        /// <param name="limit">Maximum number of messages to retrieve (default: 10000).</param>
        /// <returns>A list of messages containing the keyword.</returns>
        public List<SyslogMessage> GetMessagesByKeyword(string keyword, int limit = 10000)
        {
            var messages = new List<SyslogMessage>();

            using var command = _connection.CreateCommand();
            command.CommandText = $"""
                                                       SELECT Id, Timestamp, Hostname, AppName, ProcessId, MessageId, 
                                                              Severity, Facility, SeverityName, FacilityName, Message, FullMessage, ReceivedTime
                                                       FROM SyslogMessages
                                                       WHERE Message LIKE @keyword OR FullMessage LIKE @keyword
                                                       ORDER BY ReceivedTime DESC
                                                       LIMIT {limit}

                                   """;
            command.Parameters.AddWithValue("@keyword", $"%{keyword}%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(MapReaderToMessage(reader));
            }

            return messages;
        }

        /// <summary>
        /// Gets the total count of syslog messages in the database.
        /// </summary>
        /// <returns>The total number of messages stored.</returns>
        public int GetTotalMessageCount()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM SyslogMessages";
            return Convert.ToInt32(command.ExecuteScalar() ?? 0);
        }

        /// <summary>
        /// Deletes syslog messages older than the specified number of days.
        /// Useful for database maintenance and storage management.
        /// </summary>
        /// <param name="keepLastDays">Number of days of messages to keep (default: 7).</param>
        public void ClearOldMessages(int keepLastDays = 7)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-keepLastDays);

            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM SyslogMessages WHERE ReceivedTime < @cutoffDate";
            command.Parameters.AddWithValue("@cutoffDate", cutoffDate.ToString("o"));
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Deletes all syslog messages from the database.
        /// This operation is irreversible and should be used with caution.
        /// </summary>
        public void DeleteAllMessages()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM SyslogMessages";
            command.ExecuteNonQuery();
        }

        #region ColorRules Methods

        /// <summary>
        /// Saves a color rule to the database.
        /// If the rule ID is 0, a new rule is created; otherwise, the existing rule is updated.
        /// Also saves or updates all associated color rule conditions.
        /// </summary>
        /// <param name="rule">The color rule to save.</param>
        public void SaveColorRule(ColorRule rule)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                                                      INSERT OR REPLACE INTO ColorRules 
                                                      (Id, Name, MatchType, MatchValue, BackgroundColor, ForegroundColor, IsActive, Priority)
                                                      VALUES (@id, @name, @matchType, @matchValue, @bgColor, @fgColor, @isActive, @priority)

                                  """;

            // Add parameters (use DBNull.Value for new rules without ID)
            command.Parameters.AddWithValue("@id", rule.Id > 0 ? rule.Id : DBNull.Value);
            command.Parameters.AddWithValue("@name", rule.Name ?? "");
            command.Parameters.AddWithValue("@matchType", ""); // Placeholder for future use
            command.Parameters.AddWithValue("@matchValue", ""); // Placeholder for future use
            command.Parameters.AddWithValue("@bgColor", rule.Format.BackgroundColor ?? "");
            command.Parameters.AddWithValue("@fgColor", rule.Format.ForegroundColor ?? "");
            command.Parameters.AddWithValue("@isActive", rule.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@priority", rule.Priority);

            command.ExecuteNonQuery();

            // Retrieve the auto-generated ID if this is a new rule
            if (rule.Id == 0)
            {
                using var idCommand = _connection.CreateCommand();
                idCommand.CommandText = "SELECT last_insert_rowid()";
                rule.Id = Convert.ToInt32(idCommand.ExecuteScalar() ?? 0);
            }

            // Save or update the rule's conditions
            SaveColorRuleConditions(rule);
        }

        /// <summary>
        /// Saves the conditions associated with a color rule to the database.
        /// Deletes existing conditions and inserts new ones for atomic updates.
        /// </summary>
        /// <param name="rule">The color rule whose conditions should be saved.</param>
        private void SaveColorRuleConditions(ColorRule rule)
        {
            // Delete existing conditions for this rule to ensure clean replacement
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM ColorRuleConditions WHERE RuleId = @ruleId";
                command.Parameters.AddWithValue("@ruleId", rule.Id);
                command.ExecuteNonQuery();
            }

            // Insert the new conditions
            if (rule.Conditions.Count > 0)
            {
                using var command = _connection.CreateCommand();
                foreach (var condition in rule.Conditions)
                {
                    command.CommandText = 
                     """
                        INSERT INTO ColorRuleConditions 
                        (RuleId, PropertyName, ComparisonType, ComparisonValue, CaseSensitive, AlternativeValues)
                        VALUES (@ruleId, @propName, @compType, @compValue, @caseSens, @altValues)
                     """;

                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@ruleId", rule.Id);
                    command.Parameters.AddWithValue("@propName", condition.PropertyName ?? "");
                    command.Parameters.AddWithValue("@compType", (int)condition.ComparisonType);
                    command.Parameters.AddWithValue("@compValue", condition.ComparisonValue ?? "");
                    command.Parameters.AddWithValue("@caseSens", condition.CaseSensitive ? 1 : 0);
                    command.Parameters.AddWithValue("@altValues", string.Join("|", condition.AlternativeValues));

                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Retrieves all color rules from the database, sorted by priority in ascending order.
        /// Automatically loads all conditions associated with each rule.
        /// </summary>
        /// <returns>A list of all color rules with their conditions.</returns>
        public List<ColorRule> GetAllColorRules()
        {
            var rules = new List<ColorRule>();

            using var command = _connection.CreateCommand();
            command.CommandText = """
                                                      SELECT Id, Name, BackgroundColor, ForegroundColor, IsActive, Priority
                                                      FROM ColorRules
                                                      ORDER BY Priority ASC

                                  """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var rule = new ColorRule
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Name = reader["Name"].ToString() ?? "",
                    Format = new ColorFormat(
                        reader["BackgroundColor"].ToString() ?? "",
                        reader["ForegroundColor"].ToString() ?? ""
                    ),
                    IsActive = Convert.ToInt32(reader["IsActive"]) == 1,
                    Priority = Convert.ToInt32(reader["Priority"])
                };

                // Load conditions for this rule
                LoadColorRuleConditions(rule);
                rules.Add(rule);
            }

            return rules;
        }

        /// <summary>
        /// Loads all conditions associated with a specific color rule from the database.
        /// Populates the rule's Conditions collection.
        /// </summary>
        /// <param name="rule">The color rule whose conditions should be loaded.</param>
        private void LoadColorRuleConditions(ColorRule rule)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                                                      SELECT PropertyName, ComparisonType, ComparisonValue, CaseSensitive, AlternativeValues
                                                      FROM ColorRuleConditions
                                                      WHERE RuleId = @ruleId

                                  """;
            command.Parameters.AddWithValue("@ruleId", rule.Id);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var condition = new ColorCondition
                {
                    PropertyName = reader["PropertyName"].ToString() ?? "",
                    ComparisonType = (ComparisonType)Convert.ToInt32(reader["ComparisonType"]),
                    ComparisonValue = reader["ComparisonValue"].ToString() ?? "",
                    CaseSensitive = Convert.ToInt32(reader["CaseSensitive"]) == 1
                };

                // Parse alternative values from pipe-separated string
                var altValuesStr = reader["AlternativeValues"].ToString();
                if (!string.IsNullOrEmpty(altValuesStr))
                {
                    var altValues = altValuesStr.Split('|');
                    foreach (var alt in altValues)
                    {
                        if (!string.IsNullOrEmpty(alt))
                            condition.AlternativeValues.Add(alt);
                    }
                }

                rule.Conditions.Add(condition);
            }
        }

        /// <summary>
        /// Deletes a color rule and all its associated conditions from the database.
        /// Uses a transaction to ensure atomic operation.
        /// </summary>
        /// <param name="ruleId">The ID of the rule to delete.</param>
        public void DeleteColorRule(int ruleId)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                // Delete all conditions for this rule
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ColorRuleConditions WHERE RuleId = @ruleId";
                    command.Parameters.AddWithValue("@ruleId", ruleId);
                    command.ExecuteNonQuery();
                }

                // Delete the rule itself
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ColorRules WHERE Id = @ruleId";
                    command.Parameters.AddWithValue("@ruleId", ruleId);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        #endregion

        /// <summary>
        /// Maps a SqliteDataReader row to a SyslogMessage object.
        /// Handles type conversions and null values appropriately.
        /// </summary>
        /// <param name="reader">The data reader positioned at a valid row.</param>
        /// <returns>A SyslogMessage object populated from the reader's current row.</returns>
        private SyslogMessage MapReaderToMessage(SqliteDataReader reader)
        {
            return new SyslogMessage
            {
                Id = Convert.ToInt32(reader["Id"]),
                Timestamp = DateTime.Parse(reader["Timestamp"].ToString() ?? ""),
                Hostname = reader["Hostname"].ToString(),
                AppName = reader["AppName"].ToString(),
                ProcessId = Convert.ToInt32(reader["ProcessId"]),    
                MessageId = reader["MessageId"].ToString(),
                Severity = Convert.ToInt32(reader["Severity"]),
                Facility = Convert.ToInt32(reader["Facility"]),
                SeverityName = reader["SeverityName"].ToString(),
                FacilityName = reader["FacilityName"].ToString(),
                Message = reader["Message"].ToString(),
                FullMessage = reader["FullMessage"].ToString(),
                ReceivedTime = DateTime.Parse(reader["ReceivedTime"].ToString() ?? "")
            };    
        }

        /// <summary>
        /// Saves multiple syslog messages to the database using optimized bulk insertion.
        /// Uses parameterized queries and a transaction for best performance and safety.
        /// </summary>
        /// <param name="messages">The enumerable of messages to save.</param>
        public void SaveMessagesBulk(IEnumerable<SyslogMessage> messages)
        {
            var syslogMessages = messages.ToList();
            if (syslogMessages.Count == 0) return; // Skip if no messages to insert

            using var transaction = _connection.BeginTransaction();
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                                                  INSERT INTO SyslogMessages 
                                                  (Timestamp, Hostname, AppName, ProcessId, MessageId, Severity, Facility, 
                                                   SeverityName, FacilityName, Message, FullMessage, ReceivedTime)
                                                  VALUES (@timestamp, @hostname, @appName, @processId, @messageId, 
                                                          @severity, @facility, @severityName, @facilityName, 
                                                          @message, @fullMessage, @receivedTime)

                                  """;

            // Pre-create parameters for reuse (optimization)
            var pTimestamp = command.Parameters.Add("@timestamp", SqliteType.Text);
            var pHostname = command.Parameters.Add("@hostname", SqliteType.Text);
            var pAppName = command.Parameters.Add("@appName", SqliteType.Text);
            var pProcessId = command.Parameters.Add("@processId", SqliteType.Integer);
            var pMessageId = command.Parameters.Add("@messageId", SqliteType.Text);
            var pSeverity = command.Parameters.Add("@severity", SqliteType.Integer);
            var pFacility = command.Parameters.Add("@facility", SqliteType.Integer);
            var pSeverityName = command.Parameters.Add("@severityName", SqliteType.Text);
            var pFacilityName = command.Parameters.Add("@facilityName", SqliteType.Text);
            var pMessage = command.Parameters.Add("@message", SqliteType.Text);
            var pFullMessage = command.Parameters.Add("@fullMessage", SqliteType.Text);
            var pReceivedTime = command.Parameters.Add("@receivedTime", SqliteType.Text);

            try
            {
                // Reuse the command object for each message (performance optimization)
                foreach (var message in syslogMessages)
                {
                    pTimestamp.Value = message.Timestamp.ToString("o");
                    pHostname.Value = message.Hostname ?? "";
                    pAppName.Value = message.AppName ?? "";
                    pProcessId.Value = message.ProcessId;
                    pMessageId.Value = message.MessageId ?? "";
                    pSeverity.Value = message.Severity;
                    pFacility.Value = message.Facility;
                    pSeverityName.Value = message.SeverityName ?? "";
                    pFacilityName.Value = message.FacilityName ?? "";
                    pMessage.Value = message.Message ?? "";
                    pFullMessage.Value = message.FullMessage ?? "";
                    pReceivedTime.Value = message.ReceivedTime.ToString("o");

                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Retrieves the last N syslog messages from the database in chronological order.
        /// Returns the most recent messages available.
        /// </summary>
        /// <param name="count">The number of most recent messages to retrieve.</param>
        /// <returns>A list of the last N messages ordered chronologically (oldest first).</returns>
        public List<SyslogMessage> GetLastMessages(int count)
        {
            var messages = new List<SyslogMessage>(count);

            using (var command = _connection.CreateCommand())
            {
                command.CommandText = $"""
                                                   SELECT Id, Timestamp, Hostname, AppName, ProcessId, MessageId, 
                                                          Severity, Facility, SeverityName, FacilityName, Message, FullMessage, ReceivedTime
                                                   FROM SyslogMessages
                                                   ORDER BY Id DESC
                                                   LIMIT {count}
                                       """;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        messages.Add(MapReaderToMessage(reader));
                    }
                }
            }

            // Reverse to get chronological order (oldest to newest)
            messages.Reverse();
            return messages;
        }

        /// <summary>
        /// Purges old messages from the database, keeping only the most recent N messages.
        /// Used for managing database size and preventing unbounded growth.
        /// </summary>
        /// <param name="keepCount">The number of most recent messages to retain.</param>
        public void PurgeOldMessages(int keepCount)
        {
            using var command = _connection.CreateCommand();

            command.CommandText = $"""
                                               DELETE FROM SyslogMessages 
                                               WHERE Id NOT IN (
                                                   SELECT Id FROM SyslogMessages 
                                                   ORDER BY Id DESC 
                                                   LIMIT {keepCount}
                                               );
                                   """;
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes a custom SQL SELECT query against the database and returns results as SyslogMessage objects.
        /// Validates that the query is a SELECT statement for safety (INSERT/UPDATE/DELETE are rejected).
        /// Maps each result row to a SyslogMessage object where possible.
        /// </summary>
        /// <param name="sqlQuery">The SQL SELECT query to execute.</param>
        /// <returns>A list of SyslogMessage objects from the query results.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the query is not a SELECT statement.</exception>
        public List<SyslogMessage> ExecuteCustomQuery(string sqlQuery)
        {
            var messages = new List<SyslogMessage>();

            // Validate that the query is a SELECT statement (basic safety check)
            var trimmedQuery = sqlQuery.Trim();
            if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only SELECT queries are supported. INSERT, UPDATE, DELETE, and DROP operations are not allowed.");
            }

            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = sqlQuery;

                // Set a reasonable command timeout to prevent long-running queries
                command.CommandTimeout = 30;

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Attempt to map available columns; handle missing columns gracefully
                    try
                    {
                        var message = new SyslogMessage();

                        // Map columns if they exist; otherwise use defaults
                        if (reader.GetOrdinal("Id") >= 0)
                            message.Id = Convert.ToInt32(reader["Id"] ?? 0);

                        if (reader.GetOrdinal("Timestamp") >= 0)
                            message.Timestamp = DateTime.TryParse(reader["Timestamp"]?.ToString(), out var ts) ? ts : DateTime.UtcNow;

                        if (reader.GetOrdinal("Hostname") >= 0)
                            message.Hostname = reader["Hostname"]?.ToString();

                        if (reader.GetOrdinal("AppName") >= 0)
                            message.AppName = reader["AppName"]?.ToString();

                        if (reader.GetOrdinal("ProcessId") >= 0)
                            message.ProcessId = Convert.ToInt32(reader["ProcessId"] ?? 0);

                        if (reader.GetOrdinal("MessageId") >= 0)
                            message.MessageId = reader["MessageId"]?.ToString();

                        if (reader.GetOrdinal("Severity") >= 0)
                            message.Severity = Convert.ToInt32(reader["Severity"] ?? 0);

                        if (reader.GetOrdinal("Facility") >= 0)
                            message.Facility = Convert.ToInt32(reader["Facility"] ?? 0);

                        if (reader.GetOrdinal("SeverityName") >= 0)
                            message.SeverityName = reader["SeverityName"]?.ToString();

                        if (reader.GetOrdinal("FacilityName") >= 0)
                            message.FacilityName = reader["FacilityName"]?.ToString();

                        if (reader.GetOrdinal("Message") >= 0)
                            message.Message = reader["Message"]?.ToString();

                        if (reader.GetOrdinal("FullMessage") >= 0)
                            message.FullMessage = reader["FullMessage"]?.ToString();

                        if (reader.GetOrdinal("ReceivedTime") >= 0)
                            message.ReceivedTime = DateTime.TryParse(reader["ReceivedTime"]?.ToString(), out var rt) ? rt : DateTime.UtcNow;

                        messages.Add(message);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other rows
                        Debug.WriteLine($"Error mapping query result row: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error executing custom query: {ex.Message}", ex);
            }

            return messages;
        }

        /// <summary>
        /// Synchronously disposes the database connection and releases all unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _connection.Dispose();
        }

        /// <summary>
        /// Asynchronously disposes the database connection and releases all unmanaged resources.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}