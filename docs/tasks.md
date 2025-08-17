# Improvement Tasks Checklist

1. [ ] Add a .gitignore to exclude bin/, obj/, .idea/.vs, and other generated artifacts from source control.
2. [ ] Remove committed bin/ and obj/ directories from the repository history and working tree.
3. [ ] Add a LICENSE file to clarify usage and distribution terms.
4. [ ] Create/expand README with project purpose, build instructions, usage examples, and TLS notes.
5. [ ] Add an .editorconfig and adopt consistent code style (naming, formatting, newline, UTF-8 without BOM).

6. [ ] Enable Nullable Reference Types (Nullable=enable) in the project and fix resulting nullability issues.
7. [ ] Turn on .NET analyzers (AnalysisMode=AllEnabledByDefault) and treat warnings as errors in CI.
8. [ ] Address/waive analyzer warnings to keep the codebase warning-free.

9. [ ] Introduce an abstraction over networking (e.g., IConnection or wrapper over TcpClient/Stream) for testability.
10. [ ] Separate IRC protocol logic from transport: create an IrcClient that uses a transport abstraction instead of mixing concerns.

11. [x] EventTcpClient: Fix double-connect bug — use the injected TcpClient consistently; do not create a second TcpClient.
12. [x] EventTcpClient: Ensure _reader/_writer streams are derived from the same TcpClient managed by the instance and disposed correctly.
13. [ ] EventTcpClient: Respect the useTls parameter and use SslClientAuthenticationOptions with SNI and a CancellationToken-aware AuthenticateAsClientAsync overload.
14. [ ] EventTcpClient: Improve cancellation flow — link tokens, dispose CTS, and ensure background read task stops on cancel without leaking.
15. [ ] EventTcpClient: Define and document event semantics (when Connected/Disconnected fire). Raise Disconnected when the read loop ends or on explicit Disconnect, exactly once.
16. [ ] EventTcpClient: Make Write an async method (WriteAsync) that accepts a CancellationToken and no-ops/fails fast after disconnect.
17. [ ] EventTcpClient: Serialize concurrent writes (SemaphoreSlim or dedicated writer queue) to avoid interleaving and ObjectDisposed exceptions.
18. [x] EventTcpClient: Align StreamReader/StreamWriter leaveOpen semantics to avoid prematurely disposing the underlying stream.
19. [ ] EventTcpClient: Distinguish between normal closure (remote disconnect) and errors; surface via Error event only when appropriate.
20. [ ] EventTcpClient: Add TCP keep-alive and configurable timeouts (connect/read/write) via options.

21. [ ] TLS: Use default certificate validation and expose an optional callback to customize/relax rules (document risks). Ensure SNI uses the target host.
22. [ ] TLS: Default to TLS unless explicitly disabled; ensure the CLI flag is correctly applied to the connection path.

23. [x] Program: Fix PASS command typo — send "PASS {pass}" instead of "$PASS {pass}".
24. [x] Program: Respect --tls/--notls by passing the parsed useTls value to ConnectAsync (remove hardcoded true).
25. [ ] Program: Correct slash-command parsing to avoid removing the leading slash twice and handle commands robustly (QUIT, PART, JOIN, etc.).
26. [ ] Program: Add graceful shutdown on Ctrl+C — await disconnect, flush pending writes, and ensure Disconnected is observed.
27. [ ] Program: Allow configuration via environment variables or a config file for server, port, nick, user, real, and TLS.

28. [ ] IRC domain: Implement a minimal IRC message parser/formatter (prefix, command, params, trailing) instead of ad-hoc string operations.
29. [ ] IRC domain: Add handlers/events for common messages (PING/PONG, NOTICE, PRIVMSG, JOIN/PART, ERROR) in the protocol layer.
30. [ ] IRC domain: Implement basic rate limiting for outgoing messages to avoid server flood-kick (configurable).

31. [ ] Testing: Add unit tests for command formatting (PASS/NICK/USER), slash-command mapping, and PING/PONG behavior.
32. [ ] Testing: Add integration tests with a loopback TCP server that simulates IRC server responses (non-TLS path for speed).
33. [ ] Testing: Add tests for TLS-enabled path using mocks/fakes for SslStream or by abstracting the handshake behind an interface.
34. [ ] Testing: Verify event sequencing and lifecycle (Connected -> Data -> Disconnected), including cancellation and error paths.
35. [ ] Testing: Add concurrency tests ensuring write serialization and no race conditions during disconnect.

36. [ ] Logging: Introduce Microsoft.Extensions.Logging; avoid Console I/O in library code. Make log level configurable.
37. [ ] Observability: Add structured logging and optional redaction for sensitive data (e.g., PASS). Consider basic counters via System.Diagnostics.Metrics.

38. [ ] Performance: Make buffer sizes configurable and minimize allocations in the read loop; consider System.IO.Pipelines if throughput becomes a goal.
39. [ ] Performance: Avoid Task.Run for the read loop; use a dedicated background Task bound to the instance lifecycle.

40. [ ] CI: Add GitHub Actions workflow to build, run tests, and publish code coverage (Coverlet + ReportGenerator) on PRs.
41. [ ] Maintenance: Add Dependabot/Renovate for automated dependency updates.
42. [ ] Tooling: Add dotnet format and include style/analysis checks in CI quality gates.

43. [ ] Packaging: Provide release packaging instructions and optionally a dotnet tool package for easy installation.

44. [ ] Documentation: Document connection lifecycle, TLS behavior, and error/exit semantics.
45. [ ] Documentation: Add CONTRIBUTING.md and CODE_OF_CONDUCT.md to support external contributions.
