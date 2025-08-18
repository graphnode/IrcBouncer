using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace IrcBouncer.Server;

/// <summary>
/// Bouncer server scaffolding: accepts downstream TCP clients, enforces a connection limit,
/// and provides placeholders for downstream TLS handling. This is not a full implementation yet.
/// </summary>
public sealed class BouncerServer : IDisposable
{
    private readonly BouncerOptions _options;
    private readonly SessionRegistry _sessions;
    private readonly ILogger _logger;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _connectionSlots;
    private Task? _acceptLoopTask;
    private bool _disposed;

    public BouncerServer(BouncerOptions options, SessionRegistry sessions, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_options.TryValidate(out var error))
            throw new ArgumentException(error ?? "Invalid options", nameof(options));

        _connectionSlots = new SemaphoreSlim(_options.MaxSessions, _options.MaxSessions);
    }

    /// <summary>
    /// Starts the downstream accept loop.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_listener is not null)
            throw new InvalidOperationException("Server already started");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ip = IPAddress.TryParse(_options.BindAddress, out var parsed) ? parsed : IPAddress.Any;
        _listener = new TcpListener(ip, _options.BindPort);
        _listener.Start();

        _logger.LogInformation("BouncerServer listening on {Bind}:{Port} (TLS={Tls})", _options.BindAddress, _options.BindPort, _options.DownstreamTls);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Requests a graceful stop: stops accepting new clients and cancels pending operations.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var cts = _cts;
        if (cts is null)
            return;

        await cts.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();
        try
        {
            if (_acceptLoopTask is not null)
                await _acceptLoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ignore on shutdown
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        var listener = _listener ?? throw new InvalidOperationException("Listener not initialized");
        while (!ct.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);

                if (!await _connectionSlots.WaitAsync(0, ct).ConfigureAwait(false))
                {
                    _logger.LogWarning("Connection refused: at capacity ({MaxSessions})", _options.MaxSessions);
                    client.Dispose();
                    client = null;
                    continue;
                }

                _ = HandleClientAsync(client, ct).ContinueWith(_ => _connectionSlots.Release(), TaskScheduler.Default);
            }
            catch (OperationCanceledException)
            {
                client?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                client?.Dispose();
                _logger.LogError(ex, "Accept loop error");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            using var networkStream = client.GetStream();

            // Placeholder for downstream TLS handling
            if (_options.DownstreamTls)
            {
                // NOTE: Actual TLS handshake will be implemented later with SslStream and a server certificate.
                // For scaffolding, do nothing here.
            }

            // Placeholder: integrate session creation and routing in later tasks.
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BouncerServer));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _listener?.Stop();
            _cts?.Cancel();
            _acceptLoopTask?.GetAwaiter().GetResult();
        }
        catch
        {
            // best-effort cleanup
        }
        finally
        {
            _cts?.Dispose();
            _connectionSlots.Dispose();
        }
    }
}
