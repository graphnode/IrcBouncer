using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests;

[TestClass]
public class IrcClientTests
{
    private class MockConnection : IConnection
    {
        public List<string> SentMessages { get; } = new();
        public bool IsConnected { get; private set; }

        public event EventHandler? Connected;
        public event EventHandler<string>? Data;
        public event EventHandler<Exception>? ConnectionError;
        public event EventHandler? Disconnected;

        public Task ConnectAsync(string host, int port, bool useTls, CancellationToken? cancellationToken)
        {
            IsConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task Write(string line)
        {
            return Write(line, CancellationToken.None);
        }

        public Task Write(string line, CancellationToken cancellationToken)
        {
            if (IsConnected)
            {
                SentMessages.Add(line);
                Console.WriteLine($"[DEBUG_LOG] MockConnection sent: {line}");
            }
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            IsConnected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public void SimulateIncomingData(string data)
        {
            Data?.Invoke(this, data);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }

    [TestMethod]
    public async Task IrcClient_ConnectAsync_WithPassword_SendsCorrectAuthSequence()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);

        // Act
        await client.ConnectAsync("irc.example.com", 6667, false, "testnick", "testuser", "Test User", "testpass");

        // Assert
        Assert.AreEqual(3, mockConnection.SentMessages.Count);
        Assert.AreEqual("PASS testpass", mockConnection.SentMessages[0]);
        Assert.AreEqual("NICK testnick", mockConnection.SentMessages[1]);
        Assert.AreEqual("USER testuser 0 * :Test User", mockConnection.SentMessages[2]);

        Console.WriteLine("[DEBUG_LOG] Auth sequence verification completed");
    }

    [TestMethod]
    public async Task IrcClient_ConnectAsync_WithoutPassword_SendsCorrectAuthSequence()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);

        // Act
        await client.ConnectAsync("irc.example.com", 6667, false, "testnick", "testuser", "Test User");

        // Assert
        Assert.AreEqual(2, mockConnection.SentMessages.Count);
        Assert.AreEqual("NICK testnick", mockConnection.SentMessages[0]);
        Assert.AreEqual("USER testuser 0 * :Test User", mockConnection.SentMessages[1]);

        Console.WriteLine("[DEBUG_LOG] Auth sequence without password verification completed");
    }

    [TestMethod]
    public async Task IrcClient_PING_AutomaticallyRespondsWithPONG()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);
        await client.ConnectAsync("irc.example.com", 6667, false, "testnick", "testuser", "Test User");

        // Clear initial auth messages
        mockConnection.SentMessages.Clear();

        // Act
        mockConnection.SimulateIncomingData("PING :irc.example.com");

        // Wait a moment for async processing
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, mockConnection.SentMessages.Count);
        Assert.AreEqual("PONG :irc.example.com", mockConnection.SentMessages[0]);

        Console.WriteLine("[DEBUG_LOG] PING/PONG behavior verification completed");
    }

    [TestMethod]
    public async Task IrcClient_PING_WithComplexServer_RespondsCorrectly()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);
        await client.ConnectAsync("irc.example.com", 6667, false, "testnick", "testuser", "Test User");

        mockConnection.SentMessages.Clear();

        // Act
        mockConnection.SimulateIncomingData("PING :server.example.org");

        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, mockConnection.SentMessages.Count);
        Assert.AreEqual("PONG :server.example.org", mockConnection.SentMessages[0]);

        Console.WriteLine("[DEBUG_LOG] Complex PING/PONG behavior verification completed");
    }

    [TestMethod]
    public async Task IrcClient_SendAsync_SendsRawCommand()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);
        await client.ConnectAsync("irc.example.com", 6667, false, "testnick", "testuser", "Test User");

        mockConnection.SentMessages.Clear();

        // Act
        await client.SendAsync("JOIN #testchannel");

        // Assert
        Assert.AreEqual(1, mockConnection.SentMessages.Count);
        Assert.AreEqual("JOIN #testchannel", mockConnection.SentMessages[0]);

        Console.WriteLine("[DEBUG_LOG] Raw command sending verification completed");
    }

    [TestMethod]
    public async Task IrcClient_CommandFormatting_PassCommand_FormatsCorrectly()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);

        // Act
        await client.ConnectAsync("irc.example.com", 6667, false, "nick", "user", "real", "secret123");

        // Assert - Check PASS command formatting
        var passCommand = mockConnection.SentMessages.FirstOrDefault(m => m.StartsWith("PASS"));
        Assert.IsNotNull(passCommand);
        Assert.AreEqual("PASS secret123", passCommand);

        Console.WriteLine("[DEBUG_LOG] PASS command formatting verification completed");
    }

    [TestMethod]
    public async Task IrcClient_CommandFormatting_NickCommand_FormatsCorrectly()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);

        // Act
        await client.ConnectAsync("irc.example.com", 6667, false, "MyNick123", "user", "real");

        // Assert - Check NICK command formatting
        var nickCommand = mockConnection.SentMessages.FirstOrDefault(m => m.StartsWith("NICK"));
        Assert.IsNotNull(nickCommand);
        Assert.AreEqual("NICK MyNick123", nickCommand);

        Console.WriteLine("[DEBUG_LOG] NICK command formatting verification completed");
    }

    [TestMethod]
    public async Task IrcClient_CommandFormatting_UserCommand_FormatsCorrectly()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);

        // Act
        await client.ConnectAsync("irc.example.com", 6667, false, "nick", "myuser", "My Real Name");

        // Assert - Check USER command formatting
        var userCommand = mockConnection.SentMessages.FirstOrDefault(m => m.StartsWith("USER"));
        Assert.IsNotNull(userCommand);
        Assert.AreEqual("USER myuser 0 * :My Real Name", userCommand);

        Console.WriteLine("[DEBUG_LOG] USER command formatting verification completed");
    }

    [TestMethod]
    public async Task IrcClient_ConnectAsync_FiresConnectedEvent()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);
        var connectedFired = false;
        client.Connected += (_, _) => connectedFired = true;

        // Act
        await client.ConnectAsync("irc.example.com", 6667, false, "nick", "user", "real");

        // Assert
        Assert.IsTrue(connectedFired);

        Console.WriteLine("[DEBUG_LOG] Connected event firing verification completed");
    }

    [TestMethod]
    public void IrcClient_Disconnect_CallsConnectionDisconnect()
    {
        // Arrange
        var mockConnection = new MockConnection();
        var client = new IrcClient(mockConnection);

        // Act
        client.Disconnect();

        // Assert
        Assert.IsFalse(mockConnection.IsConnected);

        Console.WriteLine("[DEBUG_LOG] Disconnect behavior verification completed");
    }
}
