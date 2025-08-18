namespace IrcBouncer.Server;

/// <summary>
/// Routes IRC lines between multiple downstream sessions and a single upstream connection.
/// Implementations should ensure serialized writes and apply backpressure if needed.
/// </summary>
public interface IRouter
{
    /// <summary>
    /// Registers a downstream session to receive upstream lines and to contribute downstream lines.
    /// </summary>
    void AddSession(IDownstreamSession session);

    /// <summary>
    /// Unregisters a downstream session.
    /// </summary>
    void RemoveSession(string sessionId);

    /// <summary>
    /// Called when a line arrives from upstream; should be fanned out to all sessions.
    /// </summary>
    Task FanOutAsync(string upstreamLine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a line from a session to be sent upstream; implementations should serialize writes.
    /// </summary>
    Task FanInAsync(string lineFromSession, string sessionId, CancellationToken cancellationToken = default);
}
