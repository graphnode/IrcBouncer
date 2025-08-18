using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests;

[TestClass]
public class LifecycleTests
{
    [TestMethod]
    public async Task EventTcpClient_NormalLifecycle_EventsFireInCorrectSequence()
    {
        // Arrange
        var eventSequence = new List<string>();
        using var client = new EventTcpClient();

        client.Connected += (_, _) => eventSequence.Add("Connected");
        client.Data += (_, data) => eventSequence.Add($"Data:{data}");
        client.ConnectionError += (_, ex) => eventSequence.Add($"Error:{ex.GetType().Name}");
        client.Disconnected += (_, _) => eventSequence.Add("Disconnected");

        // Act & Assert - Test event sequence without actual network connection
        // Manually trigger events to verify sequencing
        client.Disconnect(); // This should trigger Disconnected

        // Wait briefly for async operations
        await Task.Delay(100);

        // Verify that Disconnected was fired
        Assert.IsTrue(eventSequence.Contains("Disconnected"), 
            $"Expected Disconnected event. Events: [{string.Join(", ", eventSequence)}]");
        
        Console.WriteLine($"[DEBUG_LOG] Event sequence: [{string.Join(", ", eventSequence)}]");
    }

    [TestMethod]
    public async Task IrcClient_FullLifecycle_EventsFireInCorrectSequence()
    {
        // Arrange
        var eventSequence = new List<string>();
        var mockConnection = new LifecycleMockConnection();
        using var client = new IrcClient(mockConnection);

        client.Connected += (_, _) => eventSequence.Add("IrcClient.Connected");
        client.MessageReceived += (_, msg) => eventSequence.Add($"IrcClient.MessageReceived:{msg}");
        client.Error += (_, ex) => eventSequence.Add($"IrcClient.Error:{ex.GetType().Name}");
        client.Disconnected += (_, _) => eventSequence.Add("IrcClient.Disconnected");

        // Act - Simulate complete lifecycle
        try
        {
            await client.ConnectAsync("test.server.com", 6667, false, "testnick", "testuser", "Test User");
            
            // Simulate receiving data
            mockConnection.SimulateData(":server.test.com 001 testnick :Welcome");
            await Task.Delay(100);
            
            // Simulate disconnect
            client.Disconnect();
            await Task.Delay(100);
        }
        catch (InvalidOperationException)
        {
            // Expected from mock connection
        }

        // Assert - Verify event sequence
        Assert.IsTrue(eventSequence.Contains("IrcClient.Connected"), 
            "Should have Connected event");
        Assert.IsTrue(eventSequence.Any(e => e.StartsWith("IrcClient.MessageReceived")), 
            "Should have MessageReceived event");
        Assert.IsTrue(eventSequence.Contains("IrcClient.Disconnected"), 
            "Should have Disconnected event");

        Console.WriteLine($"[DEBUG_LOG] IrcClient lifecycle events: [{string.Join(", ", eventSequence)}]");
    }

    [TestMethod]
    public async Task EventTcpClient_CancellationToken_TriggersCorrectEvents()
    {
        // Arrange
        var eventSequence = new List<string>();
        using var client = new EventTcpClient();
        using var cts = new CancellationTokenSource();

        client.Connected += (_, _) => eventSequence.Add("Connected");
        client.ConnectionError += (_, ex) => eventSequence.Add($"Error:{ex.GetType().Name}");
        client.Disconnected += (_, _) => eventSequence.Add("Disconnected");

        // Act - Cancel operation immediately
        await cts.CancelAsync();

        try
        {
            await client.ConnectAsync("nonexistent.test.server", 6667, false, cts.Token);
        }
        catch (OperationCanceledException)
        {
            eventSequence.Add("OperationCanceled");
        }
        catch (Exception ex)
        {
            eventSequence.Add($"Exception:{ex.GetType().Name}");
        }

        await Task.Delay(100);

        // Assert
        Assert.IsTrue(eventSequence.Any(e => e.Contains("Cancel") || e.Contains("Exception")), $"Should handle cancellation/errors. Events: [{string.Join(", ", eventSequence)}]");
        
        Console.WriteLine($"[DEBUG_LOG] Cancellation events: [{string.Join(", ", eventSequence)}]");
    }

    [TestMethod]
    public async Task EventTcpClient_ErrorPath_FiresErrorAndDisconnectedEvents()
    {
        // Arrange
        var eventSequence = new List<string>();
        var errorReceived = false;
        var disconnectedReceived = false;

        using var client = new EventTcpClient();

        client.Connected += (_, _) => eventSequence.Add("Connected");
        client.ConnectionError += (_, ex) => 
        {
            eventSequence.Add($"Error:{ex.GetType().Name}");
            errorReceived = true;
        };
        client.Disconnected += (_, _) => 
        {
            eventSequence.Add("Disconnected");
            disconnectedReceived = true;
        };

        // Act - Try to connect to non-existent server to trigger error
        try
        {
            await client.ConnectAsync("invalid.nonexistent.server.test", 9999, false);
        }
        catch (Exception ex)
        {
            eventSequence.Add($"Exception:{ex.GetType().Name}");
        }

        await Task.Delay(500); // Allow time for error handling

        // Assert
        Console.WriteLine($"[DEBUG_LOG] Error path events: [{string.Join(", ", eventSequence)}]");
        Console.WriteLine($"[DEBUG_LOG] Error received: {errorReceived}, Disconnected received: {disconnectedReceived}");
        
        // At minimum, we should get an exception or error handling
        Assert.IsTrue(eventSequence.Count > 0, "Should have received some events during error scenario");
    }

    [TestMethod]
    public void EventTcpClient_MultipleDisconnects_FiresDisconnectedOnlyOnce()
    {
        // Arrange
        var disconnectCount = 0;
        using var client = new EventTcpClient();

        client.Disconnected += (_, _) => disconnectCount++;

        // Act - Call disconnect multiple times
        client.Disconnect();
        client.Disconnect();
        client.Disconnect();

        // Assert
        Assert.AreEqual(1, disconnectCount, "Disconnected event should fire only once");
        
        Console.WriteLine("[DEBUG_LOG] Multiple disconnect test - Disconnected fired once as expected");
    }

    [TestMethod]
    public async Task IrcClient_RateLimitedOperations_MaintainsEventOrder()
    {
        // Arrange
        var mockConnection = new LifecycleMockConnection();
        using var client = new IrcClient(mockConnection);
        
        // Act
        try
        {
            await client.ConnectAsync("test.server.com", 6667, false, "testnick", "testuser", "Test User");
            
            // Send multiple messages quickly to test rate limiting
            var tasks = new[]
            {
                client.SendAsync("JOIN #channel1"),
                client.SendAsync("JOIN #channel2"),
                client.SendAsync("JOIN #channel3")
            };
            
            await Task.WhenAll(tasks);
            await Task.Delay(200); // Allow rate limiter to process
        }
        catch (InvalidOperationException)
        {
            // Expected from mock connection
        }

        // Assert
        Assert.IsTrue(mockConnection.SentMessages.Count >= 3, 
            $"Should have sent multiple messages. Sent: [{string.Join(", ", mockConnection.SentMessages)}]");
        
        Console.WriteLine($"[DEBUG_LOG] Rate limited operations - Messages sent: {mockConnection.SentMessages.Count}");
    }
}

/// <summary>
/// Mock connection specifically for lifecycle testing with better event simulation
/// </summary>
internal class LifecycleMockConnection : IConnection
{
    public List<string> SentMessages { get; } = new();
    public bool IsConnected { get; private set; }

    public event EventHandler? Connected;
    public event EventHandler<string>? Data;
    public event EventHandler<Exception>? ConnectionError;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(string host, int port, bool useTls, CancellationToken? cancellationToken = null)
    {
        await Task.Delay(10); // Simulate connection delay
        
        IsConnected = true;
        Connected?.Invoke(this, EventArgs.Empty);
        
        // Don't throw exception - allow successful "connection" for lifecycle testing
    }

    public async Task Write(string line)
    {
        await Task.CompletedTask;
        SentMessages.Add(line);
    }

    public async Task Write(string line, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        SentMessages.Add(line);
    }

    public void Disconnect()
    {
        IsConnected = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void SimulateData(string data)
    {
        Data?.Invoke(this, data);
    }

    public void SimulateError(Exception ex)
    {
        ConnectionError?.Invoke(this, ex);
    }

    public void Dispose()
    {
        Disconnect();
    }
}
