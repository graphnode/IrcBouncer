namespace IrcBouncer.Server;

/// <summary>
/// Represents a downstream client session connected to the bouncer.
/// Responsible for reading/writing framed IRC lines and exposing lifecycle events.
/// This is an abstraction to allow in-memory fakes in tests.
/// </summary>
public interface IDownstreamSession : IDisposable
{
    /// <summary>
    /// Unique identifier for the session, useful for correlation and logging.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Fired when a complete IRC line is received from the downstream client.
    /// </summary>
    event EventHandler<string>? LineReceived;

    /// <summary>
    /// Fired when the session is closed (either by client or server-side disposal).
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Starts the session processing (read loop). Implementation should be cancellation-aware.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a single IRC line (CRLF will be appended by implementation).
    /// </summary>
    Task SendAsync(string line, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a graceful shutdown of the session.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
