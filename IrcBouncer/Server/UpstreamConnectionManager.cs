namespace IrcBouncer.Server;

/// <summary>
/// Wraps EventTcpClient to expose an upstream IRC connection via IUpstreamConnection.
/// Ensures proper event mapping and lifecycle management.
/// </summary>
public sealed class UpstreamConnectionManager : IUpstreamConnection
{
    private readonly EventTcpClient _client;
    private bool _disposed;

    public UpstreamConnectionManager() : this(new EventTcpClient()) { }

    public UpstreamConnectionManager(EventTcpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _client.Data += OnClientData;
        _client.Disconnected += OnClientDisconnected;
        _client.Connected += (_, _) => IsConnected = true;
        _client.ConnectionError += (_, _) => { /* errors are not surfaced here; router/server may subscribe to EventTcpClient directly if needed */ };
    }

    public bool IsConnected { get; private set; }

    public event EventHandler<string>? LineReceived;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(string host, int port, bool useTls, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _client.ConnectAsync(host, port, useTls, cancellationToken).ConfigureAwait(false);
        // IsConnected will be set when the Connected event fires.
    }

    public Task SendAsync(string line, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _client.Write(line, cancellationToken);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _client.Disconnect();
        return Task.CompletedTask;
    }

    private void OnClientData(object? sender, string line)
    {
        try
        {
            LineReceived?.Invoke(this, line);
        }
        catch
        {
            // Swallow exceptions from handlers to avoid tearing down upstream by accident.
        }
    }

    private void OnClientDisconnected(object? sender, EventArgs e)
    {
        IsConnected = false;
        try
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // ignore
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UpstreamConnectionManager));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _client.Data -= OnClientData;
        _client.Disconnected -= OnClientDisconnected;

        _client.Dispose();
    }
}
