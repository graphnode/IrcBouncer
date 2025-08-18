using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IrcBouncer.Tests;

[TestClass]
public class IntegrationTests
{
    [TestMethod]
    public async Task IrcClient_ConnectAndAuthenticate_WithLoopbackServer()
    {
        // Arrange
        using var mockServer = new MockIrcServer();
        await mockServer.StartAsync();

        using var connection = new EventTcpClient();
        using var client = new IrcClient(connection);

        var connected = false;
        client.Connected += (_, _) => connected = true;

        // Act
        await client.ConnectAsync(mockServer.Host, mockServer.Port, false, "testnick", "testuser", "Test User", "testpass");

        // Wait for connection and initial messages
        await Task.Delay(500);

        // Assert
        Assert.IsTrue(connected, "Client should have connected successfully");
        Assert.IsTrue(mockServer.ReceivedMessages.Count >= 3, "Server should have received PASS, NICK, and USER commands");
        Assert.AreEqual("PASS testpass", mockServer.ReceivedMessages[0]);
        Assert.AreEqual("NICK testnick", mockServer.ReceivedMessages[1]);
        Assert.AreEqual("USER testuser 0 * :Test User", mockServer.ReceivedMessages[2]);

        Console.WriteLine("[DEBUG_LOG] Loopback server integration test completed");
    }

    [TestMethod]
    public async Task IrcClient_PingPongExchange_WithLoopbackServer()
    {
        // Arrange
        using var mockServer = new MockIrcServer();
        await mockServer.StartAsync();

        using var connection = new EventTcpClient();
        using var client = new IrcClient(connection);

        await client.ConnectAsync(mockServer.Host, mockServer.Port, false, "testnick", "testuser", "Test User");

        // Wait for connection to stabilize and authentication to complete
        await Task.Delay(1000);

        // Clear all messages received so far (auth messages)
        mockServer.ReceivedMessages.Clear();

        // Act - Send PING from server
        await mockServer.SendMessageAsync("PING :test.server.com");

        // Wait for the automatic PONG response
        await Task.Delay(1000);

        // Assert - Check that the server received the PONG response
        Console.WriteLine($"[DEBUG_LOG] Messages received by server: [{string.Join(", ", mockServer.ReceivedMessages)}]");

        Assert.IsTrue(mockServer.ReceivedMessages.Count > 0,
            $"Server should have received PONG response. Received {mockServer.ReceivedMessages.Count} messages: [{string.Join(", ", mockServer.ReceivedMessages)}]");

        var pongMessage = mockServer.ReceivedMessages.FirstOrDefault(m => m.StartsWith("PONG"));
        Assert.IsNotNull(pongMessage, $"Should have received a PONG message. Messages: [{string.Join(", ", mockServer.ReceivedMessages)}]");
        Assert.AreEqual("PONG :test.server.com", pongMessage);

        Console.WriteLine("[DEBUG_LOG] PING/PONG integration test completed");
    }

    [TestMethod]
    public async Task IrcClient_SendMessage_WithLoopbackServer()
    {
        // Arrange
        using var mockServer = new MockIrcServer();
        await mockServer.StartAsync();

        using var connection = new EventTcpClient();
        using var client = new IrcClient(connection);

        await client.ConnectAsync(mockServer.Host, mockServer.Port, false, "testnick", "testuser", "Test User");
        await Task.Delay(200);

        // Clear initial auth messages
        mockServer.ReceivedMessages.Clear();

        // Act
        await client.SendAsync("JOIN #testchannel");
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, mockServer.ReceivedMessages.Count);
        Assert.AreEqual("JOIN #testchannel", mockServer.ReceivedMessages[0]);

        Console.WriteLine("[DEBUG_LOG] Send message integration test completed");
    }
}

internal class MockIrcServer(int port) : IDisposable
{
    private TcpListener? _listener;
    private readonly List<TcpClient?> _clients = new();
    private readonly List<NetworkStream?> _clientStreams = new();
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public string Host => ((IPEndPoint)_listener!.LocalEndpoint).Address.ToString();
    public int Port => ((IPEndPoint)_listener!.LocalEndpoint).Port;

    public List<string> ReceivedMessages { get; } = new();

    public MockIrcServer() : this(0) { }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _cts = new CancellationTokenSource();

        _serverTask = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _clients.Add(client);

                    var stream = client.GetStream();
                    _clientStreams.Add(stream);

                    // Handle client in background
                    _ = Task.Run(() => HandleClientAsync(stream, _cts.Token), _cts.Token);
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected when server is stopped
            }
            catch (InvalidOperationException)
            {
                // Expected when server is stopped
            }
        }, _cts.Token);

        // Give the server a moment to start
        await Task.Delay(50);
    }

    private async Task HandleClientAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

            while (!cancellationToken.IsCancellationRequested && stream.CanRead)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                    break;

                lock (ReceivedMessages)
                {
                    ReceivedMessages.Add(line);
                }

                // Simulate IRC server welcome sequence after NICK/USER
                if (line.StartsWith("USER"))
                {
                    await SendWelcomeSequenceAsync(stream);
                }
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            // Expected exceptions during shutdown
        }
    }

    private async Task SendWelcomeSequenceAsync(NetworkStream stream)
    {
        try
        {
            var messages = new[]
            {
                ":test.server.com 001 testnick :Welcome to the test IRC network",
                ":test.server.com 002 testnick :Your host is test.server.com",
                ":test.server.com 003 testnick :This server was created",
                ":test.server.com 004 testnick test.server.com test-1.0 DOQRSXstuw CFILPQbcefgijklmnopqrstvz bkloveqjfI"
            };

            await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.AutoFlush = true;

            foreach (var message in messages)
            {
                await writer.WriteLineAsync(message).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // Expected during shutdown
        }
    }

    public async Task SendMessageAsync(string message)
    {
        try
        {
            foreach (var stream in _clientStreams.ToList())
            {
                if (stream?.CanWrite != true)
                    continue;

                await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                writer.AutoFlush = true;
                await writer.WriteLineAsync(message).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // Expected during shutdown
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();

        foreach (var stream in _clientStreams)
            stream?.Dispose();

        foreach (var client in _clients)
            client?.Dispose();

        _listener?.Stop();
        _cts?.Dispose();

        try
        {
            _serverTask?.Wait(1000);
        }
        catch (AggregateException)
        {
            // Expected during shutdown
        }

        _serverTask?.Dispose();
    }
}
