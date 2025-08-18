using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests;

[TestClass]
public class ConcurrencyTests
{
    [TestMethod]
    public async Task EventTcpClient_ConcurrentWritesAndDisconnect_NoRaceConditions()
    {
        // Arrange
        using var client = new EventTcpClient();
        var writeExceptions = new List<Exception>();
        var disconnectExceptions = new List<Exception>();
        var writeTasks = new List<Task>();

        // Act - Start multiple concurrent writes
        for (var i = 0; i < 20; i++)
        {
            var index = i;
            writeTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.Write($"MESSAGE {index}");
                }
                catch (Exception ex)
                {
                    lock (writeExceptions)
                    {
                        writeExceptions.Add(ex);
                    }
                }
            }));
        }

        // Trigger disconnect while writes are happening
        var disconnectTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(10); // Let some writes start
                client.Disconnect();
            }
            catch (Exception ex)
            {
                lock (disconnectExceptions)
                {
                    disconnectExceptions.Add(ex);
                }
            }
        });

        // Wait for all operations to complete
        await Task.WhenAll(writeTasks.Concat([disconnectTask]));

        // Assert - Should handle race conditions gracefully
        Console.WriteLine($"[DEBUG_LOG] Write exceptions: {writeExceptions.Count}");
        Console.WriteLine($"[DEBUG_LOG] Disconnect exceptions: {disconnectExceptions.Count}");
        
        // Disconnect exceptions should be zero (disconnect should be safe)
        Assert.AreEqual(0, disconnectExceptions.Count, 
            $"Disconnect should not throw exceptions. Exceptions: {string.Join(", ", disconnectExceptions.Select(e => e.Message))}");
        
        // Write exceptions are acceptable (connection closed, disposed, etc.)
        // but should be specific expected types
        foreach (var ex in writeExceptions)
        {
            Assert.IsTrue(ex is ObjectDisposedException || 
                         ex is InvalidOperationException || 
                         ex is OperationCanceledException,
                         $"Unexpected exception type: {ex.GetType().Name}: {ex.Message}");
        }
        
        Console.WriteLine("[DEBUG_LOG] Concurrent writes and disconnect race condition test completed");
    }

    [TestMethod]
    public async Task IrcClient_ConcurrentSendAndDisconnect_HandlesGracefully()
    {
        // Arrange
        var mockConnection = new ConcurrencyMockConnection();
        using var client = new IrcClient(mockConnection);
        var sendExceptions = new List<Exception>();
        var sendTasks = new List<Task>();

        try
        {
            await client.ConnectAsync("test.server.com", 6667, false, "testnick", "testuser", "Test User");
        }
        catch (InvalidOperationException)
        {
            // Expected from mock connection
        }

        // Act - Start multiple concurrent sends
        for (var i = 0; i < 15; i++)
        {
            var index = i;
            sendTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.SendAsync($"PRIVMSG #test :Message {index}");
                }
                catch (Exception ex)
                {
                    lock (sendExceptions)
                    {
                        sendExceptions.Add(ex);
                    }
                }
            }));
        }

        // Disconnect while sends are happening
        await Task.Delay(5);
        client.Disconnect();

        // Wait for all send operations to complete
        await Task.WhenAll(sendTasks);

        // Assert
        Console.WriteLine($"[DEBUG_LOG] Concurrent sends: {sendTasks.Count}, Exceptions: {sendExceptions.Count}");
        Console.WriteLine($"[DEBUG_LOG] Messages sent to mock: {mockConnection.SentMessages.Count}");
        
        // Should handle concurrent operations gracefully
        foreach (var ex in sendExceptions)
        {
            Assert.IsTrue(ex is ObjectDisposedException || 
                         ex is InvalidOperationException,
                         $"Unexpected exception type: {ex.GetType().Name}: {ex.Message}");
        }
        
        Console.WriteLine("[DEBUG_LOG] IrcClient concurrent send and disconnect test completed");
    }

    [TestMethod]
    public async Task EventTcpClient_MultipleDisconnectCalls_ThreadSafe()
    {
        // Arrange
        using var client = new EventTcpClient();
        var disconnectCount = 0;
        var exceptions = new List<Exception>();

        client.Disconnected += (_, _) => Interlocked.Increment(ref disconnectCount);

        var disconnectTasks = new List<Task>();

        // Act - Call disconnect from multiple threads simultaneously
        for (var i = 0; i < 10; i++)
        {
            disconnectTasks.Add(Task.Run(() =>
            {
                try
                {
                    client.Disconnect();
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

        await Task.WhenAll(disconnectTasks);
        await Task.Delay(100); // Allow event handlers to complete

        // Assert
        Assert.AreEqual(0, exceptions.Count, 
            $"Multiple disconnects should not throw exceptions. Exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        Assert.AreEqual(1, disconnectCount, 
            "Disconnected event should fire exactly once regardless of multiple calls");
        
        Console.WriteLine("[DEBUG_LOG] Multiple disconnect thread safety test completed");
    }

    [TestMethod]
    public async Task EventTcpClient_DisposeDuringOperations_ThreadSafe()
    {
        // Arrange
        var client = new EventTcpClient();
        var operationExceptions = new List<Exception>();
        var operationTasks = new List<Task>();

        // Act - Start operations and dispose simultaneously
        for (var i = 0; i < 5; i++)
        {
            var index = i;
            operationTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.Write($"TEST {index}");
                }
                catch (Exception ex)
                {
                    lock (operationExceptions)
                    {
                        operationExceptions.Add(ex);
                    }
                }
            }));
        }

        // Dispose while operations are running
        var disposeTask = Task.Run(async () =>
        {
            await Task.Delay(5);
            client.Dispose();
        });

        await Task.WhenAll(operationTasks.Concat([disposeTask]));

        // Assert
        Console.WriteLine($"[DEBUG_LOG] Operation exceptions during dispose: {operationExceptions.Count}");
        
        // All exceptions should be ObjectDisposedException or similar
        foreach (var ex in operationExceptions)
        {
            Assert.IsTrue(ex is ObjectDisposedException || 
                         ex is InvalidOperationException,
                         $"Expected disposal-related exception, got: {ex.GetType().Name}: {ex.Message}");
        }
        
        Console.WriteLine("[DEBUG_LOG] Dispose during operations thread safety test completed");
    }
}

/// <summary>
/// Mock connection for concurrency testing
/// </summary>
internal class ConcurrencyMockConnection : IConnection
{
    public List<string> SentMessages { get; } = [];
    private volatile bool _isConnected;

    public event EventHandler? Connected;
    public event EventHandler<string>? Data;
    public event EventHandler<Exception>? ConnectionError;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(string host, int port, bool useTls, CancellationToken? cancellationToken = null)
    {
        await Task.Delay(1);
        _isConnected = true;
        Connected?.Invoke(this, EventArgs.Empty);
    }

    public async Task Write(string line)
    {
        await Task.CompletedTask;
        if (_isConnected)
        {
            lock (SentMessages)
            {
                SentMessages.Add(line);
            }
        }
        else
        {
            throw new InvalidOperationException("Not connected");
        }
    }

    public async Task Write(string line, CancellationToken cancellationToken)
    {
        await Write(line);
    }

    public void Disconnect()
    {
        _isConnected = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Disconnect();
    }
}
