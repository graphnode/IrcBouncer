namespace IrcBouncer.Server;

/// <summary>
/// Abstraction for an upstream IRC connection managed by the bouncer.
/// Typically implemented by wrapping EventTcpClient + IrcClient.
/// </summary>
public interface IUpstreamConnection : IDisposable
{
    /// <summary>
    /// True if currently connected to the upstream server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Fired when a raw IRC line is received from upstream.
    /// </summary>
    event EventHandler<string>? LineReceived;

    /// <summary>
    /// Fired when the upstream connection is closed.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Connects to upstream host/port with optional TLS.
    /// </summary>
    Task ConnectAsync(string host, int port, bool useTls, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a raw IRC line upstream.
    /// </summary>
    Task SendAsync(string line, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a graceful disconnect.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
