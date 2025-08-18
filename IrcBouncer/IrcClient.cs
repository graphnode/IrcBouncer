namespace IrcBouncer;

/// <summary>
/// IRC client that handles protocol logic while using an abstract connection for transport.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types")]
public sealed class IrcClient : IDisposable
{
    private readonly IConnection _connection;
    private readonly RateLimiter _rateLimiter;
    private bool _disposed;

    /// <summary>
    /// Fired when successfully connected and authenticated to the IRC server.
    /// </summary>
    public event EventHandler? Connected;

    /// <summary>
    /// Fired when a raw message is received from the IRC server (after PING/PONG handling).
    /// </summary>
    public event EventHandler<string>? MessageReceived;

    /// <summary>
    /// Fired when a PRIVMSG is received from the IRC server.
    /// </summary>
    public event EventHandler<IrcPrivmsgEventArgs>? PrivmsgReceived;

    /// <summary>
    /// Fired when a NOTICE is received from the IRC server.
    /// </summary>
    public event EventHandler<IrcNoticeEventArgs>? NoticeReceived;

    /// <summary>
    /// Fired when someone joins a channel.
    /// </summary>
    public event EventHandler<IrcJoinEventArgs>? UserJoined;

    /// <summary>
    /// Fired when someone parts a channel.
    /// </summary>
    public event EventHandler<IrcPartEventArgs>? UserParted;

    /// <summary>
    /// Fired when an IRC ERROR message is received from the server.
    /// </summary>
    public event EventHandler<IrcErrorEventArgs>? IrcError;

    /// <summary>
    /// Fired when an error occurs in the connection or IRC protocol handling.
    /// </summary>
    public event EventHandler<Exception>? Error;

    /// <summary>
    /// Fired when disconnected from the IRC server.
    /// </summary>
    public event EventHandler? Disconnected;

    public IrcClient(IConnection connection) : this(connection, new RateLimitOptions()) { }

    public IrcClient(IConnection connection, RateLimitOptions rateLimitOptions)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _rateLimiter = new RateLimiter(rateLimitOptions ?? throw new ArgumentNullException(nameof(rateLimitOptions)));
        
        _connection.Connected += OnConnectionConnected;
        _connection.Data += OnConnectionData;
        _connection.ConnectionError += OnConnectionError;
        _connection.Disconnected += OnConnectionDisconnected;
    }

    /// <summary>
    /// Connects to an IRC server with the specified parameters.
    /// </summary>
    public Task ConnectAsync(string server, int port, bool useTls, string nick, string user, string real, string? pass = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Store connection parameters for authentication
            Nick = nick;
            User = user;
            Real = real;
            Pass = pass;
            
            _ = _connection.ConnectAsync(server, port, useTls, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a raw IRC command to the server with rate limiting applied.
    /// </summary>
    /// <param name="command">The IRC command to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task SendAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            // Apply rate limiting before sending
            await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            await _connection.Write(command, cancellationToken).ConfigureAwait(false);
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

    private async void OnConnectionData(object? sender, string rawMessage)
    {
        try
        {
            // Parse the IRC message using structured parser
            var ircMessage = IrcMessage.Parse(rawMessage);
            
            // Handle PING/PONG automatically
            if (ircMessage.Command == "PING")
            {
                var pongMessage = IrcMessage.Create("PONG", ircMessage.Trailing);
                await _connection.Write(pongMessage.Format()).ConfigureAwait(false);
                return;
            }
            
            // Emit structured events based on message type
            switch (ircMessage.Command)
            {
                case "PRIVMSG":
                    PrivmsgReceived?.Invoke(this, new IrcPrivmsgEventArgs(rawMessage, ircMessage));
                    break;
                    
                case "NOTICE":
                    NoticeReceived?.Invoke(this, new IrcNoticeEventArgs(rawMessage, ircMessage));
                    break;
                    
                case "JOIN":
                    UserJoined?.Invoke(this, new IrcJoinEventArgs(rawMessage, ircMessage));
                    break;
                    
                case "PART":
                    UserParted?.Invoke(this, new IrcPartEventArgs(rawMessage, ircMessage));
                    break;
                    
                case "ERROR":
                    IrcError?.Invoke(this, new IrcErrorEventArgs(rawMessage, ircMessage));
                    break;
            }
            
            // Always emit the raw message event for backward compatibility and other message types
            MessageReceived?.Invoke(this, rawMessage);
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
        _rateLimiter.Dispose();
        _disposed = true;
    }
}
