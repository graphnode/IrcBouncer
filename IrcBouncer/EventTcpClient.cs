using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace IrcBouncer;

public class EventTcpClient(TcpClient client) : IDisposable
{
    public event EventHandler? Connected;
    public event EventHandler<string>? Data;
    public event EventHandler<Exception>? Error;
    public event EventHandler? Disconnected;
    
    private StreamWriter? _writer;
    private StreamReader? _reader;
    
    public EventTcpClient() : this(new TcpClient())
    {
    }

    public async Task ConnectAsync(string host, int port, bool useTls = true, CancellationToken? cancellationToken = null)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken ?? CancellationToken.None);
        
        await client.ConnectAsync(host, port, cts.Token);

        Stream netStream = client.GetStream();
        if (useTls)
        {
            var ssl = new SslStream(netStream, leaveInnerStreamOpen: false);
            try
            {
                var options = new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                };
                await ssl.AuthenticateAsClientAsync(options, cts.Token);
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
                    var line = await _reader.ReadLineAsync(cts.Token);
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
                await cts.CancelAsync();
            }
        }, cts.Token);
        
        Connected?.Invoke(this, EventArgs.Empty);
        
        await readTask;
        
    }

    public async Task Write(string line)
    {
        if (_writer == null)
            return;
        
        await _writer.WriteLineAsync(line);
        await _writer.FlushAsync();
    }
    
    public void Disconnect()
    {
        client.Close();
        _writer?.Dispose();
        _reader?.Dispose();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        client.Dispose();
    }
}