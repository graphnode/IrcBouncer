namespace IrcBouncer;

/// <summary>
/// IRC client that handles protocol logic while using an abstract connection for transport.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types")]
internal sealed class IrcClient : IDisposable
{
    private readonly IConnection _connection;
    private bool _disposed;

    /// <summary>
    /// Fired when successfully connected and authenticated to the IRC server.
    /// </summary>
    public event EventHandler? Connected;

    /// <summary>
    /// Fired when a message is received from the IRC server (after PING/PONG handling).
    /// </summary>
    public event EventHandler<string>? MessageReceived;

    /// <summary>
    /// Fired when an error occurs in the connection or IRC protocol handling.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Fired when disconnected from the IRC server.
    /// </summary>
    public event EventHandler? Disconnected;

    public IrcClient(IConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        
        _connection.Connected += OnConnectionConnected;
        _connection.Data += OnConnectionData;
        _connection.ConnectionError += OnConnectionError;
        _connection.Disconnected += OnConnectionDisconnected;
    }

    /// <summary>
    /// Connects to an IRC server with the specified parameters.
    /// </summary>
    public async Task ConnectAsync(string server, int port, bool useTls, string nick, string user, string real, string? pass = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Store connection parameters for authentication
            Nick = nick;
            User = user;
            Real = real;
            Pass = pass;
            
            await _connection.ConnectAsync(server, port, useTls, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Sends a raw IRC command to the server.
    /// </summary>
    public async Task SendAsync(string command)
    {
        try
        {
            await _connection.Write(command).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the IRC server.
    /// </summary>
    public void Disconnect()
    {
        _connection.Disconnect();
    }

    private string? Nick { get; set; }
    private string? User { get; set; }
    private string? Real { get; set; }
    private string? Pass { get; set; }

    private async void OnConnectionConnected(object? sender, EventArgs e)
    {
        try
        {
            // Send authentication sequence
            if (!string.IsNullOrEmpty(Pass))
            {
                await _connection.Write($"PASS {Pass}").ConfigureAwait(false);
            }
            
            if (!string.IsNullOrEmpty(Nick))
            {
                await _connection.Write($"NICK {Nick}").ConfigureAwait(false);
            }
            
            if (!string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(Real))
            {
                await _connection.Write($"USER {User} 0 * :{Real}").ConfigureAwait(false);
            }
            
            Connected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
    }

    private async void OnConnectionData(object? sender, string message)
    {
        try
        {
            // Handle PING/PONG automatically
            if (message.StartsWith("PING ", StringComparison.OrdinalIgnoreCase))
            {
                var payload = message[5..];
                await _connection.Write($"PONG {payload}").ConfigureAwait(false);
                return;
            }
            
            // Forward other messages to consumers
            MessageReceived?.Invoke(this, message);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
    }

    private void OnConnectionError(object? sender, Exception exception)
    {
        Error?.Invoke(this, exception);
    }

    private void OnConnectionDisconnected(object? sender, EventArgs e)
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _connection.Connected -= OnConnectionConnected;
        _connection.Data -= OnConnectionData;
        _connection.ConnectionError -= OnConnectionError;
        _connection.Disconnected -= OnConnectionDisconnected;
        
        _connection.Dispose();
        _disposed = true;
    }
}
