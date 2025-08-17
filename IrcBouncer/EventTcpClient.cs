using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace IrcBouncer;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
internal class EventTcpClient : IDisposable
{
    public event EventHandler? Connected;
    public event EventHandler<string>? Data;
    public event EventHandler<Exception>? Error;
    public event EventHandler? Disconnected;
    
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly TcpClient _client = new();

    public async Task ConnectAsync(string host, int port, bool useTls = true, CancellationToken? cancellationToken = null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken ?? CancellationToken.None);
        
        await _client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);

        Stream netStream = _client.GetStream();
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
                await ssl.AuthenticateAsClientAsync(options, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Error?.Invoke(this, ex);
                return;
            }
            netStream = ssl;
        }

        _reader = new StreamReader(netStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        _writer = new StreamWriter(netStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 8192, leaveOpen: true);
        _writer.NewLine = "\r\n";
        _writer.AutoFlush = true;
        
        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var line = await _reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
                    if (line == null) break;
                    
                    Data?.Invoke(this, line);
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Error?.Invoke(this, ex);
            }
            finally
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
        }, cts.Token);
        
        Connected?.Invoke(this, EventArgs.Empty);
        
        await readTask.ConfigureAwait(false);
        
        await netStream.DisposeAsync().ConfigureAwait(false);
    }

    public async Task Write(string line)
    {
        if (_writer == null)
            return;
        
        await _writer.WriteLineAsync(line).ConfigureAwait(false);
        await _writer.FlushAsync().ConfigureAwait(false);
    }
    
    public void Disconnect()
    {
        _client.Close();
        _writer?.Dispose();
        _reader?.Dispose();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _client.Dispose();
    }
}
