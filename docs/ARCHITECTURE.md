# IrcBouncer Architecture Documentation

This document describes the connection lifecycle, TLS behavior, error handling, and exit semantics of IrcBouncer.

## Table of Contents

1. [Connection Lifecycle](#connection-lifecycle)
2. [TLS Behavior](#tls-behavior)
3. [Error Handling](#error-handling)
4. [Exit Semantics](#exit-semantics)
5. [Event System](#event-system)
6. [Threading Model](#threading-model)
7. [Resource Management](#resource-management)

## Connection Lifecycle

IrcBouncer follows a well-defined connection lifecycle with predictable state transitions and event sequences.

### Lifecycle Phases

```
[Disconnected] → [Connecting] → [Connected] → [Authenticated] → [Active] → [Disconnecting] → [Disconnected]
                      ↓              ↓            ↓          ↓         ↓
                   [Error] ←─────────────────────────────────────────┘
```

### Detailed Flow

#### 1. Connection Initiation (`ConnectAsync`)

```csharp
await ircClient.ConnectAsync(server, port, useTls, nick, user, real, pass, cancellationToken);
```

**Internal sequence:**
1. **TCP Connection**: `TcpClient.ConnectAsync()` establishes the network connection
2. **TLS Handshake** (if enabled): `SslStream.AuthenticateAsClientAsync()` performs TLS negotiation
3. **Stream Setup**: Creates `StreamReader`/`StreamWriter` with UTF-8 encoding and configurable buffer sizes
4. **Read Task Start**: Launches dedicated background task for message reading
5. **Connected Event**: Fires when connection is ready for communication
6. **IRC Authentication**: Sends `PASS`, `NICK`, and `USER` commands
7. **Welcome Message**: Waits for `001 RPL_WELCOME` to confirm authentication

#### 2. Active Communication

- **Outbound Messages**: Processed through serialized write queue to prevent interleaving
- **Inbound Messages**: Handled by dedicated read loop, parsed and dispatched as events
- **Automatic PING/PONG**: Responds to server keepalive messages automatically
- **Rate Limiting**: Applied to outbound messages to prevent server flood-kick

#### 3. Disconnection

**Graceful Shutdown (`/quit` command or Ctrl+C):**
1. Send `QUIT` message to server
2. Close write stream (no more outbound messages)
3. Wait for server to close connection or timeout (5 seconds)
4. Fire `Disconnected` event exactly once
5. Dispose all resources

**Abrupt Disconnection (network failure, server close):**
1. Read loop detects connection closure (`ReadLineAsync` returns null)
2. Fire `Disconnected` event exactly once
3. Dispose all resources

### Event Sequence Guarantees

- `Connected` fires exactly once per successful connection
- `Disconnected` fires exactly once per connection lifecycle
- `Data` events fire between `Connected` and `Disconnected`
- `ConnectionError` fires for exceptional conditions only (not normal closure)
- Events are fired on background threads; handlers should be thread-safe

## TLS Behavior

IrcBouncer implements secure-by-default TLS with comprehensive certificate validation.

### TLS Configuration

#### Default Behavior
- **TLS Enabled by Default**: Connections use TLS unless explicitly disabled with `--notls`
- **Standard Certificate Validation**: Uses .NET's default validation (CA chain, hostname matching, revocation)
- **SNI Support**: Server Name Indication is automatically configured using the target hostname
- **Modern TLS Versions**: Negotiates TLS 1.2+ (handled by .NET runtime)

#### TLS Options in `TcpConnectionOptions`

```csharp
var options = new TcpConnectionOptions
{
    CertificateValidationCallback = customValidationCallback // Optional custom validation
};
```

### Certificate Validation

#### Standard Validation (Default)
```csharp
// Uses .NET default validation
// - Verifies certificate chain to trusted CA
// - Validates hostname matches certificate
// - Checks certificate not expired/revoked
```

#### Custom Validation (Advanced)
```csharp
options.CertificateValidationCallback = (sender, cert, chain, errors) =>
{
    // Custom logic for self-signed certificates, etc.
    // WARNING: Only use in controlled environments
    return errors == SslPolicyErrors.None;
};
```

### TLS Handshake Process

1. **TCP Connection**: Establish plain TCP connection
2. **SslStream Creation**: Wrap TCP stream with SSL stream
3. **Authentication Options**: Configure SNI and validation
4. **Handshake**: Call `AuthenticateAsClientAsync()` with cancellation token
5. **Verification**: Validate certificate according to configured policy
6. **Stream Ready**: Replace network stream with authenticated SSL stream

### TLS Error Handling

- **Certificate Errors**: Logged as errors, connection attempt fails
- **Handshake Timeout**: Controlled by connection timeout setting
- **Protocol Mismatch**: Server doesn't support required TLS version
- **Cancellation**: Handshake respects cancellation tokens

### Security Considerations

⚠️ **Certificate Validation Warnings:**
- Never disable certificate validation in production
- Custom validation callbacks can introduce vulnerabilities
- Self-signed certificates should only be used in controlled environments
- Always validate the actual security implications before relaxing TLS policies

## Error Handling

IrcBouncer implements comprehensive error handling with clear error propagation and logging.

### Error Categories

#### 1. Connection Errors
- **DNS Resolution Failures**: Host not found
- **Network Connectivity**: Connection refused, timeout
- **TLS Handshake Failures**: Certificate validation, protocol mismatch

#### 2. Protocol Errors
- **Authentication Failures**: Invalid credentials, banned nick
- **Message Parsing Errors**: Malformed IRC messages
- **Rate Limiting**: Server flood protection triggered

#### 3. System Errors
- **Resource Exhaustion**: Out of memory, file handles
- **Threading Issues**: Task cancellation, synchronization
- **I/O Errors**: Stream write failures, network interruption

### Error Propagation

#### EventTcpClient Level
```csharp
client.ConnectionError += (sender, exception) =>
{
    // Exception details available for logging/handling
    logger.LogError(exception, "Connection error occurred");
};
```

#### IrcClient Level
```csharp
client.Error += (sender, exception) =>
{
    // Higher-level IRC protocol errors
    logger.LogError(exception, "IRC client error occurred");
};
```

#### Application Level
```csharp
try 
{
    await ircClient.ConnectAsync(...);
}
catch (OperationCanceledException)
{
    // Cancellation requested (Ctrl+C, timeout)
}
catch (Exception ex)
{
    // Unexpected errors
    logger.LogError(ex, "Unexpected error");
}
```

### Error Recovery

- **No Automatic Reconnection**: Application exits on connection loss
- **Graceful Degradation**: Invalid commands are logged but don't crash the application
- **Resource Cleanup**: All resources are properly disposed even during error conditions

### Logging and Observability

- **Structured Logging**: Uses Microsoft.Extensions.Logging with structured data
- **Metrics**: Connection duration, message counts, error rates via System.Diagnostics.Metrics
- **Sensitive Data Redaction**: Passwords and tokens are not logged in plain text

## Exit Semantics

IrcBouncer supports multiple exit mechanisms with proper cleanup guarantees.

### Exit Triggers

#### 1. User Commands
- **`/quit` or `/exit`**: Graceful IRC disconnect
- **EOF (Ctrl+D/Ctrl+Z)**: Console input ends
- **Empty input**: Terminates input loop

#### 2. System Signals
- **Ctrl+C (SIGINT)**: Graceful shutdown requested
- **SIGTERM**: Graceful shutdown (Unix systems)

#### 3. Error Conditions
- **Connection Lost**: Remote server disconnects
- **Authentication Failed**: Invalid credentials
- **Unhandled Exceptions**: Last resort error handling

### Exit Process

#### Graceful Shutdown Sequence

1. **Shutdown Signal**: Received from user command or system signal
2. **Set Graceful Flag**: `gracefulShutdownRequested = true`
3. **Cancel Input**: Stop accepting new console input
4. **Send QUIT**: Transmit `QUIT :Graceful shutdown` to server
5. **Wait for Disconnect**: Server acknowledges and closes connection
6. **Timeout Protection**: 5-second maximum wait for server response
7. **Resource Cleanup**: Dispose connections, streams, and background tasks
8. **Exit Code 0**: Successful shutdown

#### Emergency Shutdown Sequence

1. **Error Detected**: Unrecoverable error or timeout
2. **Cancel Operations**: Cancel all async operations immediately
3. **Force Disconnect**: Close TCP connection without IRC QUIT
4. **Resource Cleanup**: Dispose all resources (may timeout)
5. **Exit Code 1**: Error shutdown

### Resource Cleanup Guarantees

- **Deterministic Disposal**: All `IDisposable` resources are properly disposed
- **Background Task Cleanup**: Read/write tasks are cancelled and awaited
- **Stream Closure**: Network streams are flushed and closed
- **Memory Cleanup**: Large buffers are released, GC pressure reduced

### Exit Codes

- **0**: Successful operation and graceful shutdown
- **1**: Error occurred during operation or shutdown
- **2**: Invalid command-line arguments
- **130**: Interrupted by signal (Ctrl+C on Unix)

## Event System

### Thread Safety

- **Events Fire on Background Threads**: Handlers must be thread-safe
- **No Guaranteed Order**: Multiple subscribers may execute concurrently
- **Exception Isolation**: Handler exceptions don't affect other handlers

### Event Handler Best Practices

```csharp
client.Data += (sender, message) =>
{
    // Thread-safe operations only
    // Avoid blocking operations
    // Handle exceptions internally
    try 
    {
        ProcessMessage(message);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing message: {Message}", message);
    }
};
```

## Threading Model

### Background Tasks

- **Read Task**: Dedicated long-running task for reading server messages
- **Write Serialization**: `SemaphoreSlim` ensures ordered message transmission
- **Input Task**: Dedicated task for console input processing (not using `Task.Run`)

### Cancellation

- **Hierarchical Tokens**: Application → Connection → Operation level cancellation
- **Graceful Cancellation**: Background tasks respond to cancellation within reasonable time
- **Resource Disposal**: Cancellation triggers proper resource cleanup

## Resource Management

### Memory Management

- **Configurable Buffers**: StreamReader/Writer buffer sizes configurable via options
- **String Interning**: IRC commands use string constants where possible
- **Allocation Minimization**: Reuse objects where feasible, avoid unnecessary allocations

### Network Resources

- **Connection Pooling**: Not implemented (single connection per instance)
- **Keep-Alive**: TCP keep-alive configurable via connection options
- **Timeout Management**: Connect, read, and write timeouts prevent resource leaks

### Cleanup Patterns

```csharp
public void Dispose()
{
    // 1. Signal shutdown
    _connectionCts?.Cancel();
    
    // 2. Close network connection
    _client.Close();
    
    // 3. Wait for background tasks (with timeout)
    _readTask?.Wait(TimeSpan.FromSeconds(1));
    
    // 4. Dispose managed resources
    _writeSemaphore.Dispose();
    _writer?.Dispose();
    _reader?.Dispose();
    _client.Dispose();
    _connectionCts?.Dispose();
    
    // 5. Fire final events
    FireDisconnectedOnce();
}
```

This architecture ensures predictable behavior, proper resource management, and secure-by-default operation while maintaining good performance and observability.
