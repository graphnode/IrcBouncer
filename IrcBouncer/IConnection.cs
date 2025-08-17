namespace IrcBouncer;

/// <summary>
/// Abstraction over network connection for testability.
/// Provides event-driven networking with async operations.
/// </summary>
public interface IConnection : IDisposable
{
    /// <summary>
    /// Fired when the connection is successfully established.
    /// </summary>
    event EventHandler? Connected;

    /// <summary>
    /// Fired when data is received from the remote endpoint.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances")]
    event EventHandler<string>? Data;

    /// <summary>
    /// Fired when an error occurs during connection or communication.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances")]
    event EventHandler<Exception>? ConnectionError;

    /// <summary>
    /// Fired when the connection is disconnected (either explicitly or by remote).
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Connects to the specified host and port with optional TLS.
    /// </summary>
    /// <param name="host">The hostname to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="useTls">Whether to use TLS encryption.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the connection operation.</returns>
    Task ConnectAsync(string host, int port, bool useTls = true, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Writes a line of data to the remote endpoint.
    /// </summary>
    /// <param name="line">The line to write.</param>
    /// <returns>A task representing the write operation.</returns>
    Task Write(string line);

    /// <summary>
    /// Writes a line of data to the remote endpoint with cancellation support.
    /// </summary>
    /// <param name="line">The line to write.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the write operation.</returns>
    Task Write(string line, CancellationToken cancellationToken);

    /// <summary>
    /// Explicitly disconnects from the remote endpoint.
    /// </summary>
    void Disconnect();
}
