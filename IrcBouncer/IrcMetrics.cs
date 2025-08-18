using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace IrcBouncer;

/// <summary>
/// Provides metrics tracking for IRC bouncer operations.
/// </summary>
public sealed class IrcMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _connectionsTotal;
    private readonly Counter<long> _messagesSent;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _errorsTotal;
    private readonly Counter<long> _disconnectionsTotal;
    private readonly Histogram<double> _connectionDuration;
    
    private bool _disposed;
    
    public IrcMetrics()
    {
        _meter = new Meter("IrcBouncer", "1.0.0");
        
        _connectionsTotal = _meter.CreateCounter<long>(
            "irc_connections_total",
            "count",
            "Total number of IRC connection attempts");
            
        _messagesSent = _meter.CreateCounter<long>(
            "irc_messages_sent_total",
            "count", 
            "Total number of IRC messages sent");
            
        _messagesReceived = _meter.CreateCounter<long>(
            "irc_messages_received_total",
            "count",
            "Total number of IRC messages received");
            
        _errorsTotal = _meter.CreateCounter<long>(
            "irc_errors_total",
            "count",
            "Total number of IRC errors encountered");
            
        _disconnectionsTotal = _meter.CreateCounter<long>(
            "irc_disconnections_total", 
            "count",
            "Total number of IRC disconnections");
            
        _connectionDuration = _meter.CreateHistogram<double>(
            "irc_connection_duration_seconds",
            "seconds",
            "Duration of IRC connections in seconds");
    }
    
    /// <summary>
    /// Increments the connection attempts counter.
    /// </summary>
    /// <param name="server">The IRC server being connected to</param>
    /// <param name="useTls">Whether TLS is being used</param>
    public void IncrementConnections(string server, bool useTls)
    {
        if (_disposed) return;
        
        var tags = new TagList
        {
            { "server", server },
            { "tls", useTls }
        };
        
        _connectionsTotal.Add(1, tags);
    }
    
    /// <summary>
    /// Increments the messages sent counter.
    /// </summary>
    /// <param name="command">The IRC command being sent (e.g., PRIVMSG, JOIN)</param>
    /// <param name="containsSensitiveData">Whether the message contains sensitive data</param>
    public void IncrementMessagesSent(string? command = null, bool containsSensitiveData = false)
    {
        if (_disposed) return;
        
        var tags = new TagList();
        if (!string.IsNullOrEmpty(command))
        {
            tags.Add("command", command);
        }
        tags.Add("sensitive", containsSensitiveData);
        
        _messagesSent.Add(1, tags);
    }
    
    /// <summary>
    /// Increments the messages received counter.
    /// </summary>
    /// <param name="command">The IRC command received (e.g., PRIVMSG, JOIN)</param>
    public void IncrementMessagesReceived(string? command = null)
    {
        if (_disposed) return;
        
        var tags = new TagList();
        if (!string.IsNullOrEmpty(command))
        {
            tags.Add("command", command);
        }
        
        _messagesReceived.Add(1, tags);
    }
    
    /// <summary>
    /// Increments the errors counter.
    /// </summary>
    /// <param name="errorType">The type of error (e.g., connection, protocol, timeout)</param>
    public void IncrementErrors(string errorType)
    {
        if (_disposed) return;
        
        var tags = new TagList
        {
            { "error_type", errorType }
        };
        
        _errorsTotal.Add(1, tags);
    }
    
    /// <summary>
    /// Increments the disconnections counter.
    /// </summary>
    /// <param name="reason">The reason for disconnection (e.g., user, error, timeout)</param>
    public void IncrementDisconnections(string reason)
    {
        if (_disposed) return;
        
        var tags = new TagList
        {
            { "reason", reason }
        };
        
        _disconnectionsTotal.Add(1, tags);
    }
    
    /// <summary>
    /// Records the duration of a connection.
    /// </summary>
    /// <param name="durationSeconds">Connection duration in seconds</param>
    /// <param name="server">The IRC server</param>
    public void RecordConnectionDuration(double durationSeconds, string server)
    {
        if (_disposed) return;
        
        var tags = new TagList
        {
            { "server", server }
        };
        
        _connectionDuration.Record(durationSeconds, tags);
    }
    
    /// <summary>
    /// Extracts the IRC command from a message for metrics purposes.
    /// </summary>
    /// <param name="message">The IRC message</param>
    /// <returns>The command part of the message, or null if not found</returns>
    public static string? ExtractCommand(string message)
    {
        if (string.IsNullOrEmpty(message))
            return null;
            
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;
            
        // Handle messages with prefix (starting with :)
        if (parts[0].StartsWith(':') && parts.Length > 1)
            return parts[1].ToUpperInvariant();
            
        return parts[0].ToUpperInvariant();
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        _meter.Dispose();
    }
}
