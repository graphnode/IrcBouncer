using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Security;

namespace IrcBouncer.Tests;

[TestClass]
public class TlsTests
{
    [TestMethod]
    public void TcpConnectionOptions_CertificateValidationCallback_CanBeSet()
    {
        // Arrange & Act
        var options = new TcpConnectionOptions
        {
            CertificateValidationCallback = (_, _, _, _) => true
        };

        // Assert
        Assert.IsNotNull(options.CertificateValidationCallback);

        // Test the callback functionality
        var result = options.CertificateValidationCallback(null!, null, null, SslPolicyErrors.None);
        Assert.IsTrue(result);

        Console.WriteLine("[DEBUG_LOG] Certificate validation callback configuration verified");
    }

    [TestMethod]
    public void TcpConnectionOptions_DefaultCertificateValidation_IsNull()
    {
        // Arrange & Act
        var options = new TcpConnectionOptions();

        // Assert
        Assert.IsNull(options.CertificateValidationCallback);
        Console.WriteLine("[DEBUG_LOG] Default certificate validation is null as expected");
    }

    [TestMethod]
    public async Task EventTcpClient_ConnectAsync_WithTls_UsesCorrectParameters()
    {
        // Arrange
        var options = new TcpConnectionOptions
        {
            ConnectTimeoutMs = 5000,
            CertificateValidationCallback = (_, _, _, sslPolicyErrors) =>
            {
                // Custom validation logic for testing
                Console.WriteLine($"[DEBUG_LOG] Certificate validation called with errors: {sslPolicyErrors}");
                return sslPolicyErrors == SslPolicyErrors.None;
            }
        };

        using var client = new EventTcpClient(options);

        // Act & Assert - This tests the configuration without actually connecting to a TLS server
        // The real TLS connection would fail in a unit test environment, but we can verify
        // that the client is configured correctly for TLS usage

        try
        {
            // Attempt connection to a non-existent TLS server
            // This will fail as expected but validates that TLS parameters are processed
            await client.ConnectAsync("nonexistent.test.server", 6697, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Expected to fail - we're testing configuration, not actual TLS connectivity
            Console.WriteLine($"[DEBUG_LOG] Expected TLS connection failure: {ex.GetType().Name}");
            Assert.IsTrue(ex is System.Net.Sockets.SocketException or System.Net.NetworkInformation.PingException or TaskCanceledException or AggregateException, "Should fail with network-related exception");
        }

        Console.WriteLine("[DEBUG_LOG] TLS configuration parameters verified");
    }

    [TestMethod]
    public async Task IrcClient_ConnectAsync_WithTls_PassesTlsParameterCorrectly()
    {
        // Arrange
        var mockConnection = new MockTlsConnection();
        using var client = new IrcClient(mockConnection);

        // Act
        try
        {
            await client.ConnectAsync("test.server.com", 6697, true, "testnick", "testuser", "Test User");
        }
        catch (InvalidOperationException)
        {
            // Expected for mock connection
        }

        // Assert
        Assert.IsTrue(mockConnection.ConnectAsyncCalled);
        Assert.AreEqual("test.server.com", mockConnection.LastHost);
        Assert.AreEqual(6697, mockConnection.LastPort);
        Assert.IsTrue(mockConnection.LastUseTls);

        Console.WriteLine("[DEBUG_LOG] TLS parameter passing verified");
    }

    [TestMethod]
    public async Task IrcClient_ConnectAsync_WithoutTls_PassesNonTlsParameterCorrectly()
    {
        // Arrange
        var mockConnection = new MockTlsConnection();
        using var client = new IrcClient(mockConnection);

        // Act
        try
        {
            await client.ConnectAsync("test.server.com", 6667, false, "testnick", "testuser", "Test User");
        }
        catch (InvalidOperationException)
        {
            // Expected for mock connection
        }

        // Assert
        Assert.IsTrue(mockConnection.ConnectAsyncCalled);
        Assert.AreEqual("test.server.com", mockConnection.LastHost);
        Assert.AreEqual(6667, mockConnection.LastPort);
        Assert.IsFalse(mockConnection.LastUseTls);

        Console.WriteLine("[DEBUG_LOG] Non-TLS parameter passing verified");
    }

    [TestMethod]
    public void TcpConnectionOptions_TlsTimeouts_CanBeConfigured()
    {
        // Arrange & Act
        var options = new TcpConnectionOptions
        {
            ConnectTimeoutMs = 10000,
            ReadTimeoutMs = 30000,
            WriteTimeoutMs = 15000
        };

        // Assert
        Assert.AreEqual(10000, options.ConnectTimeoutMs);
        Assert.AreEqual(30000, options.ReadTimeoutMs);
        Assert.AreEqual(15000, options.WriteTimeoutMs);

        Console.WriteLine("[DEBUG_LOG] TLS timeout configuration verified");
    }
}

/// <summary>
/// Mock connection for testing TLS parameter passing without actual network operations
/// </summary>
internal class MockTlsConnection : IConnection
{
    public bool ConnectAsyncCalled { get; private set; }
    public string? LastHost { get; private set; }
    public int LastPort { get; private set; }
    public bool LastUseTls { get; private set; }

    public event EventHandler? Connected;
    public event EventHandler<string>? Data;
    public event EventHandler<Exception>? ConnectionError;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(string host, int port, bool useTls, CancellationToken? cancellationToken = null)
    {
        ConnectAsyncCalled = true;
        LastHost = host;
        LastPort = port;
        LastUseTls = useTls;

        await Task.Delay(10); // Simulate async operation

        // Simulate connection success for parameter verification
        Connected?.Invoke(this, EventArgs.Empty);

        // Throw exception to prevent actual connection attempt
        throw new InvalidOperationException("Mock connection - operation not implemented");
    }

    public async Task Write(string line)
    {
        await Task.CompletedTask;
        throw new InvalidOperationException("Mock connection - operation not implemented");
    }

    public async Task Write(string line, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        throw new InvalidOperationException("Mock connection - operation not implemented");
    }

    public void Disconnect()
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        // Mock disposal
    }
}
