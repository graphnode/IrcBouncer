using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace IrcBouncer.Server;

/// <summary>
/// Concrete downstream client session handling framed IRC lines over a stream.
/// Implements cancellation-aware read/write loops and proper disposal semantics.
/// </summary>
public sealed class ClientSession : IDownstreamSession
{
    private readonly TcpClient _client;
    private readonly Stream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ILogger? _logger;

    private Task? _readLoopTask;
    private int _disconnectedRaised;
    private int _started;
    private bool _disposed;

    public string Id { get; }

    public event EventHandler<string>? LineReceived;
    public event EventHandler? Disconnected;

    /// <summary>
    /// Creates a session using the TcpClient's network stream.
    /// </summary>
    public ClientSession(TcpClient client, ILogger? logger = null)
        : this(client, client.GetStream() ?? throw new ArgumentNullException(nameof(client)), logger)
    {
    }

    /// <summary>
    /// Creates a session using a provided transport stream (e.g., SslStream wrapped over the client).
    /// </summary>
    public ClientSession(TcpClient client, Stream transportStream, ILogger? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _stream = transportStream ?? throw new ArgumentNullException(nameof(transportStream));
        _logger = logger;

        Id = Guid.NewGuid().ToString("n");

        // Use UTF-8 without BOM; leave streams open to control lifecycle explicitly.
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _reader = new StreamReader(_stream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        _writer = new StreamWriter(_stream, encoding, bufferSize: 4096, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            // Already started; return existing task if any.
            return _readLoopTask ?? Task.CompletedTask;
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var token = linkedCts.Token;

        _readLoopTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    string? line;
                    try
                    {
                        line = await _reader.ReadLineAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break; // Stream disposed due to shutdown
                    }
                    catch (IOException)
                    {
                        // Network error or cancellation via socket close
                        break;
                    }

                    if (line is null)
                    {
                        // Remote closed
                        break;
                    }

                    try
                    {
                        LineReceived?.Invoke(this, line);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in LineReceived handler for session {SessionId}", Id);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled exception in read loop for session {SessionId}", Id);
            }
            finally
            {
                RaiseDisconnectedOnce();
                linkedCts.Dispose();
            }
        }, token);

        return _readLoopTask;
    }

    public async Task SendAsync(string line, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (line is null) throw new ArgumentNullException(nameof(line));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        try
        {
            await _sendLock.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // No-op on cancellation
        }
        catch (ObjectDisposedException)
        {
            // Ignore writes after dispose/shutdown
        }
        catch (IOException)
        {
            // Ignore network errors on send for scaffolding; router may decide on further action
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        _cts.Cancel();

        // Closing the client/stream will unblock ReadLineAsync
        try { _writer.Flush(); } catch { /* ignore */ }
        try { _stream.Close(); } catch { /* ignore */ }
        try { _client.Close(); } catch { /* ignore */ }

        var task = _readLoopTask;
        if (task is not null)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2), linked.Token)).ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }

        RaiseDisconnectedOnce();
    }

    private void RaiseDisconnectedOnce()
    {
        if (Interlocked.Exchange(ref _disconnectedRaised, 1) == 1)
            return;
        try
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in Disconnected handler for session {SessionId}", Id);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ClientSession));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();

        try { _reader.Dispose(); } catch { /* ignore */ }
        try { _writer.Dispose(); } catch { /* ignore */ }
        try { _stream.Dispose(); } catch { /* ignore */ }
        try { _client.Dispose(); } catch { /* ignore */ }

        RaiseDisconnectedOnce();
    }
}
