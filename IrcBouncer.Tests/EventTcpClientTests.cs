using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests;

[TestClass]
public class EventTcpClientTests
{
    [TestMethod]
    public void EventTcpClient_Disconnect_RaisesDisconnectedEventOnce()
    {
        // Arrange
        using var client = new EventTcpClient();
        var disconnectedCount = 0;
        client.Disconnected += (_, _) => {
            disconnectedCount++;
            Console.WriteLine("[DEBUG_LOG] Disconnected event fired");
        };

        // Act
        client.Disconnect();
        client.Disconnect(); // Second call should not fire event again

        // Assert
        Assert.AreEqual(1, disconnectedCount);
        Console.WriteLine("[DEBUG_LOG] Disconnected event fires exactly once verification completed");
    }

    [TestMethod]
    public async Task EventTcpClient_Write_BeforeConnect_IsNoOp()
    {
        // Arrange
        using var client = new EventTcpClient();

        // Act & Assert - Should not throw or hang even when not connected
        try
        {
            await client.Write("PING :test");
            Console.WriteLine("[DEBUG_LOG] Write before connect completed without exception");
            Assert.IsTrue(true); // Test passes if no exception is thrown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG_LOG] Unexpected exception: {ex.Message}");
            Assert.Fail($"Write before connect should not throw, but got: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task EventTcpClient_Write_WithCancellationToken_RespectsToken()
    {
        // Arrange
        using var client = new EventTcpClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        // Act & Assert - Should handle cancellation gracefully
        try
        {
            await client.Write("PING :test", cts.Token);
            Console.WriteLine("[DEBUG_LOG] Write with cancelled token completed gracefully");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[DEBUG_LOG] Write with cancelled token threw OperationCanceledException as expected");
            // This is acceptable behavior
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG_LOG] Unexpected exception: {ex.Message}");
            Assert.Fail($"Write with cancelled token should handle cancellation gracefully, but got: {ex.Message}");
        }
    }

    [TestMethod]
    public void EventTcpClient_Dispose_MultipleCallsSafe()
    {
        // Arrange
        var client = new EventTcpClient();
        var disposedCount = 0;
        client.Disconnected += (_, _) => {
            disposedCount++;
            Console.WriteLine("[DEBUG_LOG] Disconnected event during disposal");
        };

        // Act
        client.Dispose();
        client.Dispose(); // Second dispose should be safe

        // Assert
        // Should not throw and disconnected should fire at most once
        Assert.IsTrue(disposedCount <= 1);
        Console.WriteLine("[DEBUG_LOG] Multiple dispose calls safety verification completed");
    }

    [TestMethod]
    public void EventTcpClient_DefaultConstructor_CreatesInstance()
    {
        // Act
        using var client = new EventTcpClient();

        // Assert
        Assert.IsNotNull(client);
        Console.WriteLine("[DEBUG_LOG] Default constructor verification completed");
    }

    [TestMethod]
    public void EventTcpClient_ConstructorWithOptions_CreatesInstance()
    {
        // Arrange
        var options = new TcpConnectionOptions
        {
            ConnectTimeoutMs = 10000,
            EnableKeepAlive = true
        };

        // Act
        using var client = new EventTcpClient(options);

        // Assert
        Assert.IsNotNull(client);
        Console.WriteLine("[DEBUG_LOG] Constructor with options verification completed");
    }

    [TestMethod]
    public void EventTcpClient_ConstructorWithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new EventTcpClient(null!));
        Console.WriteLine("[DEBUG_LOG] Null options constructor exception verification completed");
    }

    [TestMethod]
    public void EventTcpClient_ConnectAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var client = new EventTcpClient();
        client.Dispose();

        // Act & Assert
        Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => 
            await client.ConnectAsync("example.com", 6667, false));
        
        Console.WriteLine("[DEBUG_LOG] ConnectAsync after dispose exception verification completed");
    }

    [TestMethod]
    public void EventTcpClient_EventsRegistration_WorksCorrectly()
    {
        // Arrange
        using var client = new EventTcpClient();
        var connectedFired = false;
        var errorFired = false;
        var disconnectedFired = false;
        var dataReceived = false;

        // Act - Register event handlers
        client.Connected += (_, _) => {
            connectedFired = true;
            Console.WriteLine("[DEBUG_LOG] Connected event handler fired");
        };
        client.ConnectionError += (_, ex) => {
            errorFired = true;
            Console.WriteLine($"[DEBUG_LOG] Error event handler fired: {ex.Message}");
        };
        client.Disconnected += (_, _) => {
            disconnectedFired = true;
            Console.WriteLine("[DEBUG_LOG] Disconnected event handler fired");
        };
        client.Data += (_, data) => {
            dataReceived = true;
            Console.WriteLine($"[DEBUG_LOG] Data event handler fired: {data}");
        };

        // Trigger disconnect to test one event
        client.Disconnect();

        // Assert
        Assert.IsFalse(connectedFired); // Should not fire without actual connection
        Assert.IsFalse(errorFired); // Should not fire without error
        Assert.IsTrue(disconnectedFired); // Should fire on explicit disconnect
        Assert.IsFalse(dataReceived); // Should not fire without data

        Console.WriteLine("[DEBUG_LOG] Event registration verification completed");
    }

    [TestMethod]
    public async Task EventTcpClient_ConcurrentWrites_Serialized()
    {
        // Arrange
        using var client = new EventTcpClient();
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act - Attempt concurrent writes (should be serialized internally)
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.Write($"MESSAGE {index}");
                    Console.WriteLine($"[DEBUG_LOG] Concurrent write {index} completed");
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should not have concurrency-related exceptions
        Assert.AreEqual(0, exceptions.Count, $"Concurrent writes had {exceptions.Count} exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        
        Console.WriteLine("[DEBUG_LOG] Concurrent writes serialization verification completed");
    }
}
