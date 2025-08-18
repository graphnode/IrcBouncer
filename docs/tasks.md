# Improvement Tasks Checklist

1. [x] Add a .gitignore to exclude bin/, obj/, .idea/.vs, and other generated artifacts from source control.
2. [x] Remove committed bin/ and obj/ directories from the repository history and working tree.
3. [x] Add a LICENSE file to clarify usage and distribution terms.
4. [x] Create/expand README with project purpose, build instructions, usage examples, and TLS notes.
5. [x] Add an .editorconfig and adopt consistent code style (naming, formatting, newline, UTF-8 without BOM).

6. [x] Enable Nullable Reference Types (Nullable=enable) in the project and fix resulting nullability issues.
7. [x] Turn on .NET analyzers (AnalysisMode=AllEnabledByDefault) and treat warnings as errors in CI.
8. [x] Address/waive analyzer warnings to keep the codebase warning-free.

9. [x] Introduce an abstraction over networking (e.g., IConnection or wrapper over TcpClient/Stream) for testability.
10. [x] Separate IRC protocol logic from transport: create an IrcClient that uses a transport abstraction instead of mixing concerns.

11. [x] EventTcpClient: Fix double-connect bug — use the injected TcpClient consistently; do not create a second TcpClient.
12. [x] EventTcpClient: Ensure _reader/_writer streams are derived from the same TcpClient managed by the instance and disposed correctly.
13. [x] EventTcpClient: Respect the useTls parameter and use SslClientAuthenticationOptions with SNI and a CancellationToken-aware AuthenticateAsClientAsync overload.
14. [x] EventTcpClient: Improve cancellation flow — link tokens, dispose CTS, and ensure background read task stops on cancel without leaking.
15. [x] EventTcpClient: Define and document event semantics (when Connected/Disconnected fire). Raise Disconnected when the read loop ends or on explicit Disconnect, exactly once.
16. [x] EventTcpClient: Make Write an async method (WriteAsync) that accepts a CancellationToken and no-ops/fails fast after disconnect.
17. [x] EventTcpClient: Serialize concurrent writes (SemaphoreSlim or dedicated writer queue) to avoid interleaving and ObjectDisposed exceptions.
18. [x] EventTcpClient: Align StreamReader/StreamWriter leaveOpen semantics to avoid prematurely disposing the underlying stream.
19. [x] EventTcpClient: Distinguish between normal closure (remote disconnect) and errors; surface via Error event only when appropriate.
20. [x] EventTcpClient: Add TCP keep-alive and configurable timeouts (connect/read/write) via options.

21. [x] TLS: Use default certificate validation and expose an optional callback to customize/relax rules (document risks). Ensure SNI uses the target host.
22. [x] TLS: Default to TLS unless explicitly disabled; ensure the CLI flag is correctly applied to the connection path.

23. [x] Program: Fix PASS command typo — send "PASS {pass}" instead of "$PASS {pass}".
24. [x] Program: Respect --tls/--notls by passing the parsed useTls value to ConnectAsync (remove hardcoded true).
25. [x] Program: Correct slash-command parsing to avoid removing the leading slash twice and handle commands robustly (QUIT, PART, JOIN, etc.).
26. [x] Program: Add graceful shutdown on Ctrl+C — await disconnect, flush pending writes, and ensure Disconnected is observed.
27. [x] Program: Allow configuration via environment variables or a config file for server, port, nick, user, real, and TLS.

28. [x] IRC domain: Implement a minimal IRC message parser/formatter (prefix, command, params, trailing) instead of ad-hoc string operations.
29. [x] IRC domain: Add handlers/events for common messages (PING/PONG, NOTICE, PRIVMSG, JOIN/PART, ERROR) in the protocol layer.
30. [x] IRC domain: Implement basic rate limiting for outgoing messages to avoid server flood-kick (configurable).

31. [x] Testing: Add unit tests for command formatting (PASS/NICK/USER), slash-command mapping, and PING/PONG behavior.
32. [x] Testing: Add integration tests with a loopback TCP server that simulates IRC server responses (non-TLS path for speed).
33. [x] Testing: Add tests for TLS-enabled path using mocks/fakes for SslStream or by abstracting the handshake behind an interface.
34. [x] Testing: Verify event sequencing and lifecycle (Connected -> Data -> Disconnected), including cancellation and error paths.
35. [x] Testing: Add concurrency tests ensuring write serialization and no race conditions during disconnect.

36. [x] Logging: Introduce Microsoft.Extensions.Logging; avoid Console I/O in library code. Make log level configurable.
37. [x] Observability: Add structured logging and optional redaction for sensitive data (e.g., PASS). Consider basic counters via System.Diagnostics.Metrics.

38. [x] Performance: Make buffer sizes configurable and minimize allocations in the read loop; consider System.IO.Pipelines if throughput becomes a goal.
39. [x] Performance: Avoid Task.Run for the read loop; use a dedicated background Task bound to the instance lifecycle.

40. [x] CI: Add GitHub Actions workflow to build, run tests, and publish code coverage (Coverlet + ReportGenerator) on PRs.
41. [x] Maintenance: Add Dependabot/Renovate for automated dependency updates.
42. [x] Tooling: Add dotnet format and include style/analysis checks in CI quality gates.

43. [x] Packaging: Provide release packaging instructions and optionally a dotnet tool package for easy installation.

44. [x] Documentation: Document connection lifecycle, TLS behavior, and error/exit semantics.
45. [x] Documentation: Add CONTRIBUTING.md and CODE_OF_CONDUCT.md to support external contributions.


## Phase 2 — Server-Side Bouncer Tasks (Multi-Client → Single Upstream)

Legend: [ ] = Not started, [*] = In progress, [x] = Done

46. [x] Architecture scaffolding: define server-side abstractions (IDownstreamSession, IUpstreamConnection, IRouter), and a lightweight SessionRegistry.
47. [x] Configuration model: bind address/port, downstream TLS enable flag, server certificate path/password, upstream host/port, upstream TLS flag (default on), shared secret for downstream auth, limits (max sessions, rate).
48. [x] Downstream listener: implement BouncerServer accept loop with CancellationToken, connection limit semaphore, and optional TLS using SslStream over TcpClient.
49. [ ] ClientSession scaffolding: per-connection read/write loops with UTF-8 + CRLF framing, safe disposal, and cancellation-aware shutdown.
50. [ ] UpstreamConnectionManager: wrap EventTcpClient to expose connect/disconnect, state, and events; ensure TLS defaults, SNI, and optional reconnect backoff.
51. [ ] Router: implement fan-in (sessions → upstream) with serialized writes and fan-out (upstream → all sessions) with backpressure handling.
52. [ ] Authentication: require PASS from downstream clients before relaying; validate against configured shared secret; handle error and disconnect flows.
53. [ ] Rate limiting and quotas: per-session basic message rate and optional burst limits to prevent abuse.
54. [x] CLI: add `serve` command and options for downstream bind/TLS/cert and upstream host/port/TLS/secret; integrate with System.CommandLine.
55. [ ] Graceful shutdown: stop accepting, notify sessions, drain queues, disconnect upstream, await tasks; ensure exactly-once Disconnected semantics.
56. [ ] Logging: structured logs with per-session correlation IDs; redact sensitive data (PASS); integrate with existing logging approach.
57. [ ] Metrics: counters for connected sessions, messages relayed, auth failures, upstream reconnects.
58. [ ] Unit tests: routing correctness (fan-in/fan-out), framing, auth gate behavior using in-memory streams/fakes.
59. [ ] Concurrency tests: multiple sessions writing concurrently; verify serialization and absence of interleaving or ObjectDisposed exceptions.
60. [ ] Integration tests: loopback downstream server and fake upstream (or mocked EventTcpClient) to validate lifecycle and shutdown.
61. [ ] CLI tests: parse/validation for `serve` options and defaults; help text checks.
62. [ ] Documentation: update README usage with `serve` examples; expand docs/ARCHITECTURE.md with server components and sequence diagrams.
63. [ ] Packaging: instructions for running the bouncer as a background process/service; publish profiles and configuration examples.
64. [ ] Security review: document TLS defaults, downstream TLS guidance, secret handling, and known limitations.
65. [ ] Acceptance: verify criteria in docs/plan.md (Phase 2) are met; ensure CI green on new tests.
