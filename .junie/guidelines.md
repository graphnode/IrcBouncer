# IrcBouncer Development Guidelines

## Build/Configuration Instructions

### Project Structure
- **Target Framework**: .NET 9.0
- **Project Type**: Console application (`<OutputType>Exe</OutputType>`)
- **Key Dependencies**: System.CommandLine (v2.0.0-beta7.25380.108)
- **Language Features**: Nullable reference types enabled, implicit usings enabled

### Build Commands
```powershell
# Build the solution
dotnet build

# Run the application
dotnet run --project IrcBouncer

# Publish for deployment
dotnet publish -c Release -o publish
```

### Configuration Notes
- The application uses System.CommandLine beta package for CLI argument parsing
- Default server: `irc.libera.chat:6697` with TLS enabled
- TLS is enabled by default unless `--notls` flag is specified
- EventTcpClient has a bug: it creates two TcpClient instances (lines 27-30 in EventTcpClient.cs should use the constructor parameter `client` instead of creating `tcp`)

## Testing Information

### Adding Tests
Create a test project with MSTest framework:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.6.0" />
    <PackageReference Include="MSTest.TestFramework" Version="3.6.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\IrcBouncer\IrcBouncer.csproj" />
  </ItemGroup>
</Project>
```

### Sample Tests
Test EventTcpClient's safe behaviors (avoid network operations in unit tests):

```csharp
using System.Threading.Tasks;
using IrcBouncer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests;

[TestClass]
public class EventTcpClientTests
{
    [TestMethod]
    public void Disconnect_Raises_Disconnected_Once()
    {
        using var client = new EventTcpClient();
        var count = 0;
        client.Disconnected += (_, _) => count++;

        client.Disconnect();

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task Write_Before_Connect_Is_NoOp()
    {
        using var client = new EventTcpClient();

        // Should not throw or hang even when not connected
        await client.Write("PING :test");
    }
}
```

### Running Tests
```powershell
# Run all tests in a specific project
dotnet test YourTestProject

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run tests in Visual Studio/Rider
# Use Test Explorer or right-click test methods/classes
```

## Additional Development Information

### Code Style & Patterns
- **Nullable Reference Types**: Enabled project-wide, use `?` for nullable parameters
- **Async/Await**: Extensive use throughout; prefer `ConfigureAwait(false)` for library code
- **Event Handling**: EventTcpClient uses standard .NET events (Connected, Data, Error, Disconnected)
- **Disposal Pattern**: Implement `IDisposable` properly; EventTcpClient disposes streams and TcpClient
- **Cancellation**: Use `CancellationToken` for async operations, `CancellationTokenSource.CreateLinkedTokenSource()` for chaining

### Network & TLS Considerations
- **SslStream**: Used for TLS connections, requires proper exception handling
- **Stream Management**: UTF-8 encoding, `\r\n` line endings for IRC protocol
- **Connection Lifecycle**: Events fired in sequence: Connected → Data (multiple) → Error/Disconnected

### Debugging Tips
- **Console Logging**: Add `Console.WriteLine($"[DEBUG] {message}")` for runtime debugging
- **Exception Handling**: EventTcpClient swallows `OperationCanceledException` but forwards others via Error event
- **IRC Protocol**: Raw IRC messages are logged with `< message` (incoming) and `> message` (outgoing)

### Windows-Specific Notes
- **Path Separators**: Use `\` for Windows paths in PowerShell commands
- **Console Input**: `Console.ReadLine()` used for interactive IRC commands
- **TLS Validation**: Default certificate validation may fail for self-signed certificates

### Known Issues
- **EventTcpClient Bug**: Constructor creates unused TcpClient instance (line 27), should reuse `client` parameter
- **Error Recovery**: No automatic reconnection logic implemented
- **Memory Management**: Streams not disposed in all error paths

### CLI Usage Examples
```powershell
# Connect to default server (libera.chat:6697 with TLS)
dotnet run

# Connect with specific parameters
dotnet run -- --server irc.example.com --port 6667 --notls --nick MyBot

# Show help
dotnet run -- --help
```

### IRC Commands
- Type raw IRC commands directly (e.g., `JOIN #channel`)
- Special commands: `/quit` to exit, `/leave` → `PART`, `/exit` → `QUIT`
- PING responses are automated