using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace IrcBouncer;

/// <summary>
/// Configuration options for EventTcpClient connection behavior.
/// </summary>
internal sealed class TcpConnectionOptions
{
    /// <summary>
    /// Connection timeout in milliseconds. Default: 30000 (30 seconds).
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Read timeout in milliseconds. 0 means no timeout. Default: 0.
    /// </summary>
    public int ReadTimeoutMs { get; set; }

    /// <summary>
    /// Write timeout in milliseconds. Default: 30000 (30 seconds).
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Whether to enable TCP keep-alive. Default: true.
    /// </summary>
    public bool EnableKeepAlive { get; set; } = true;

    /// <summary>
    /// TCP keep-alive time in milliseconds (time before first keep-alive probe). Default: 7200000 (2 hours).
    /// </summary>
    public int KeepAliveTimeMs { get; set; } = 7200000;

    /// <summary>
    /// TCP keep-alive interval in milliseconds (interval between keep-alive probes). Default: 1000 (1 second).
    /// </summary>
    public int KeepAliveIntervalMs { get; set; } = 1000;
}

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
internal sealed class EventTcpClient : IConnection
{
    /// <summary>
    /// Fired when the connection is successfully established and ready for communication.
    /// This event is fired only once per connection lifecycle, after the read task is set up.
    /// </summary>
    public event EventHandler? Connected;

    /// <summary>
    /// Fired when data is received from the remote endpoint.
    /// </summary>
    public event EventHandler<string>? Data;

    /// <summary>
    /// Fired when an error occurs during connection, authentication, or communication.
    /// Does not fire for normal connection closure.
    /// </summary>
    public event EventHandler<Exception>? ConnectionError;

    /// <summary>
    /// Fired when the connection is disconnected, either explicitly via Disconnect() or
    /// when the remote endpoint closes the connection. This event is fired exactly once
    /// per connection lifecycle.
    /// </summary>
    public event EventHandler? Disconnected;
    
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly TcpClient _client = new();
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly TcpConnectionOptions _options;
    private CancellationTokenSource? _connectionCts;
    private Task? _readTask;
    private bool _disconnectedFired;
    private bool _disposed;

    /// <summary>
    /// Initializes a new EventTcpClient with default connection options.
    /// </summary>
    public EventTcpClient() : this(new TcpConnectionOptions()) { }

    /// <summary>
    /// Initializes a new EventTcpClient with the specified connection options.
    /// </summary>
    /// <param name="options">Connection configuration options.</param>
    public EventTcpClient(TcpConnectionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Connects to the specified host and port with optional TLS encryption.
    /// The connection lifecycle: Connect -> Connected event -> Data events -> Disconnected event.
    /// </summary>
    public async Task ConnectAsync(string host, int port, bool useTls = true, CancellationToken? cancellationToken = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EventTcpClient));

        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken ?? CancellationToken.None);
        
        // Apply connection timeout
        if (_options.ConnectTimeoutMs > 0)
        {
            _connectionCts.CancelAfter(_options.ConnectTimeoutMs);
        }
        
        try
        {
            await _client.ConnectAsync(host, port, _connectionCts.Token).ConfigureAwait(false);

            // Configure TCP keep-alive after connection is established
            if (_options.EnableKeepAlive)
            {
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                
                // Set keep-alive timing (Windows-specific, gracefully handled on other platforms)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        var keepAliveValues = new byte[12];
                        BitConverter.GetBytes((uint)1).CopyTo(keepAliveValues, 0); // Enable
                        BitConverter.GetBytes((uint)_options.KeepAliveTimeMs).CopyTo(keepAliveValues, 4); // Time before first probe
                        BitConverter.GetBytes((uint)_options.KeepAliveIntervalMs).CopyTo(keepAliveValues, 8); // Interval between probes

                        _client.Client.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
                    }
                    catch
                    {
                        // Keep-alive timing configuration failed
                        // Basic keep-alive is still enabled via SetSocketOption above
                    }
                }
            }

            Stream netStream = _client.GetStream();
            
            // Apply timeouts to the network stream
            if (netStream is NetworkStream networkStream)
            {
                if (_options.ReadTimeoutMs > 0)
                    networkStream.ReadTimeout = _options.ReadTimeoutMs;
                if (_options.WriteTimeoutMs > 0)
                    networkStream.WriteTimeout = _options.WriteTimeoutMs;
            }
            
            if (useTls)
            {
#pragma warning disable CA2000
                var ssl = new SslStream(netStream, leaveInnerStreamOpen: false);
#pragma warning restore CA2000
                try
                {
                    var options = new SslClientAuthenticationOptions
                    {
                        TargetHost = host,
                    };
                    await ssl.AuthenticateAsClientAsync(options, _connectionCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await ssl.DisposeAsync().ConfigureAwait(false);
                    if (ex is not OperationCanceledException)
                        ConnectionError?.Invoke(this, ex);
                    return;
                }
                netStream = ssl;
            }

            _reader = new StreamReader(netStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: false);
            _writer = new StreamWriter(netStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 8192, leaveOpen: false);
            _writer.NewLine = "\r\n";
            _writer.AutoFlush = true;
            
            // Start the read task as a dedicated background task bound to the instance lifecycle
            _readTask = ReadLoopAsync(_connectionCts.Token);
            
            // Fire Connected event after everything is set up
            Connected?.Invoke(this, EventArgs.Empty);
            
            // Wait for the read task to complete (connection closed or cancelled)
            await _readTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                ConnectionError?.Invoke(this, ex);
        }
        finally
        {
            // Ensure Disconnected is fired exactly once
            FireDisconnectedOnce();
        }
    }

    /// <summary>
    /// Writes a line of data to the remote endpoint with optional cancellation support.
    /// Concurrent writes are serialized to avoid interleaving and ObjectDisposed exceptions.
    /// </summary>
    public async Task Write(string line, CancellationToken cancellationToken)
    {
        if (_disposed || _writer == null)
            return;
        
        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;
            
            await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Don't propagate cancellation or ObjectDisposed as errors
            if (ex is not OperationCanceledException and not ObjectDisposedException)
                ConnectionError?.Invoke(this, ex);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Explicitly disconnects from the remote endpoint.
    /// </summary>
    public void Disconnect()
    {
        if (_disposed)
            return;

        _connectionCts?.Cancel();
        _client.Close();
        
        FireDisconnectedOnce();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        _connectionCts?.Cancel();
        _client.Close();
        
        // Wait for read task to complete with a timeout
        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore exceptions during disposal
        }
        
        _writeSemaphore.Dispose();
        _writer?.Dispose();
        _reader?.Dispose();
        _client.Dispose();
        _connectionCts?.Dispose();
        
        FireDisconnectedOnce();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) 
                {
                    // Normal closure - remote end closed the connection
                    break;
                }
                
                Data?.Invoke(this, line);
            }
        }
        catch (Exception ex)
        {
            // Only report non-cancellation exceptions as errors
            if (ex is not OperationCanceledException and not ObjectDisposedException)
                ConnectionError?.Invoke(this, ex);
        }
    }

    private void FireDisconnectedOnce()
    {
        if (!_disconnectedFired)
        {
            _disconnectedFired = true;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    // Maintain backward compatibility with existing Write signature
    public Task Write(string line) => Write(line, CancellationToken.None);
}
