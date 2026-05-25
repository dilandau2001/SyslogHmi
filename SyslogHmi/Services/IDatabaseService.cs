using System.Collections.Generic;
using System.Threading.Tasks;
using SyslogHmi.Models;

namespace SyslogHmi.Services;

/// <summary>
/// Defines the contract for database operations related to syslog messages and color rules.
/// Provides methods for saving, retrieving, filtering, and managing syslog messages and color formatting rules.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Saves a list of syslog messages to the database.
    /// Uses a transaction to ensure atomic operation.
    /// </summary>
    /// <param name="messages">The list of syslog messages to save.</param>
    void SaveMessages(List<SyslogMessage> messages);

    /// <summary>
    /// Retrieves all syslog messages from the database with an optional limit.
    /// Returns messages ordered by most recent received time first.
    /// </summary>
    /// <param name="limit">The maximum number of messages to retrieve. Default is 10000.</param>
    /// <returns>A list of syslog messages.</returns>
    List<SyslogMessage> GetAllMessages(int limit = 10000);

    /// <summary>
    /// Retrieves syslog messages filtered by severity level.
    /// Returns messages ordered by most recent received time first.
    /// </summary>
    /// <param name="severity">The severity level to filter by.</param>
    /// <param name="limit">The maximum number of messages to retrieve. Default is 10000.</param>
    /// <returns>A list of syslog messages matching the specified severity.</returns>
    List<SyslogMessage> GetMessagesBySeverity(int severity, int limit = 10000);

    /// <summary>
    /// Retrieves syslog messages containing a specific keyword in message text.
    /// Searches both the short message and full message fields.
    /// Returns messages ordered by most recent received time first.
    /// </summary>
    /// <param name="keyword">The keyword to search for (case-insensitive substring match).</param>
    /// <param name="limit">The maximum number of messages to retrieve. Default is 10000.</param>
    /// <returns>A list of syslog messages containing the keyword.</returns>
    List<SyslogMessage> GetMessagesByKeyword(string keyword, int limit = 10000);

    /// <summary>
    /// Gets the total count of syslog messages in the database.
    /// </summary>
    /// <returns>The total number of messages stored.</returns>
    int GetTotalMessageCount();

    /// <summary>
    /// Deletes syslog messages older than the specified number of days.
    /// </summary>
    /// <param name="keepLastDays">The number of days to keep. Messages older than this are deleted. Default is 7.</param>
    void ClearOldMessages(int keepLastDays = 7);

    /// <summary>
    /// Deletes all syslog messages from the database.
    /// This operation is irreversible.
    /// </summary>
    void DeleteAllMessages();

    /// <summary>
    /// Saves a color rule (and its conditions) to the database.
    /// If the rule has an ID of 0, a new rule is created; otherwise, the existing rule is updated.
    /// </summary>
    /// <param name="rule">The color rule to save.</param>
    void SaveColorRule(ColorRule rule);

    /// <summary>
    /// Retrieves all color rules from the database, sorted by priority in ascending order.
    /// Also loads all conditions associated with each rule.
    /// </summary>
    /// <returns>A list of all color rules.</returns>
    List<ColorRule> GetAllColorRules();

    /// <summary>
    /// Deletes a color rule and all its associated conditions from the database.
    /// </summary>
    /// <param name="ruleId">The ID of the rule to delete.</param>
    void DeleteColorRule(int ruleId);

    /// <summary>
    /// Saves multiple syslog messages to the database using bulk insertion for improved performance.
    /// Uses a transaction to ensure atomic operation.
    /// </summary>
    /// <param name="messages">The enumerable of syslog messages to save.</param>
    void SaveMessagesBulk(IEnumerable<SyslogMessage> messages);

    /// <summary>
    /// Retrieves the last N syslog messages from the database, ordered chronologically.
    /// Returns the most recently received messages up to the specified count.
    /// </summary>
    /// <param name="count">The number of most recent messages to retrieve.</param>
    /// <returns>A list of the last N syslog messages in chronological order.</returns>
    List<SyslogMessage> GetLastMessages(int count);

    /// <summary>
    /// Deletes the oldest syslog messages from the database, keeping only the most recent N messages.
    /// Used to manage database size and memory footprint.
    /// </summary>
    /// <param name="keepCount">The number of most recent messages to keep.</param>
    void PurgeOldMessages(int keepCount);

    /// <summary>
    /// Executes a custom SQL query against the SyslogMessages table and returns results as SyslogMessage objects.
    /// Only SELECT queries are supported; other query types are rejected for safety.
    /// Results are mapped to SyslogMessage objects where possible.
    /// </summary>
    /// <param name="sqlQuery">The SQL SELECT query to execute. Should target SyslogMessages table columns.</param>
    /// <returns>A list of SyslogMessage objects resulting from the query execution.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query is not a SELECT statement.</exception>
    /// <exception cref="Exception">Thrown if the SQL query execution fails.</exception>
    List<SyslogMessage> ExecuteCustomQuery(string sqlQuery);

    /// <summary>
    /// Disposes the database connection and releases unmanaged resources.
    /// </summary>
    void Dispose();

    /// <summary>
    /// Asynchronously disposes the database connection and releases unmanaged resources.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask DisposeAsync();
}