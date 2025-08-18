# IrcBouncer

A simple, event-driven IRC client/bouncer console application built on .NET 9.0. It connects to an IRC server (TLS by default), lets you send raw IRC commands from the console, and prints incoming messages.

## Features
- Secure-by-default TLS connection (disable with `--notls`)
- Event-based transport (Connected, Data, Error, Disconnected)
- Minimal CLI using System.CommandLine
- Automatic PING/PONG handling

## Quick Start

```powershell
# Build
dotnet build

# Run with defaults (libera.chat:6697, TLS enabled)
dotnet run --project IrcBouncer

# Connect to a specific server/port without TLS
dotnet run --project IrcBouncer -- --server irc.example.com --port 6667 --notls --nick MyNick --user myuser --real "My Real Name"
```

While connected, type raw IRC lines to send. Special slash commands are supported:
- `/quit` → QUIT (disconnects)
- `/leave` → PART
- `/exit` → QUIT

## TLS Notes
- TLS is enabled by default unless `--notls` is specified.
- The client uses `SslStream` with SNI (`TargetHost`) during handshake and the default certificate validation policy.
- Certificate validation may fail for self-signed certificates.

## Build Requirements
- .NET SDK 9.0
- Windows PowerShell (repository commands use Windows-style paths)

## Configuration
You can set server, port, TLS, and identity via CLI options:
- `--server`, `--port`
- `--tls` or `--notls` (mutually exclusive; TLS is default)
- `--nick`, `--user`, `--real`, `--pass`

## Development
- Nullable Reference Types enabled
- .NET analyzers enabled (warnings treated as errors in CI)
- UTF-8 (no BOM), CRLF line endings recommended via `.editorconfig`

## License
This project is released under the MIT License. See `LICENSE` for details.
