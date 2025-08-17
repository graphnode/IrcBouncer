# IrcBouncer — Project Improvement Plan

Date: 2025-08-17

## Overview and Assumptions
- The file docs/requirements.md referenced in the task is not present in the repository at the time of writing. Therefore, this plan extracts goals and constraints from available sources:
  - docs/tasks.md (Improvement Tasks Checklist)
  - Development Guidelines provided with the project context (target framework, dependencies, known issues, testing approach, CLI usage, network/TLS considerations)
  - Current codebase (notably IrcBouncer/EventTcpClient.cs and IrcBouncer/Program.cs)
- Assumptions:
  - Primary objective is to deliver a reliable, secure-by-default IRC bouncer CLI with maintainable architecture, good observability, and solid tests/CI.
  - Windows is a first‑class environment; cross‑platform compatibility is desirable but not strictly required for this plan.
  - Non-functional priorities: correctness, resource safety, predictable event semantics, testability, and documentation.
  - Once docs/requirements.md becomes available, this plan should be revisited to align with the authoritative requirements.

## Extracted Goals
1. Provide a secure-by-default IRC connectivity experience (TLS on by default, proper certificate validation, SNI).
2. Ensure robust networking lifecycle and event semantics (Connected → Data → Disconnected) with clear error reporting.
3. Improve architecture for testability and separation of concerns (transport vs. protocol logic).
4. Offer a usable CLI with correct flag handling, validation, and graceful shutdown.
5. Add a minimal IRC message parser/formatter and basic protocol behaviors (e.g., PING/PONG) in a protocol layer.
6. Establish comprehensive testing (unit + integration), focusing on lifecycle, concurrency, and protocol correctness without real network dependencies.
7. Enhance logging/observability (structured logs, sensitive data redaction) and performance where practical.
8. Adopt repository hygiene, CI, and maintenance automation (formatting, analyzers, Dependabot/Renovate, coverage reports).
9. Improve documentation (README, lifecycle docs, CONTRIBUTING, CODE_OF_CONDUCT, licensing).

## Constraints
- Technical
  - .NET 9.0; Console application.
  - System.CommandLine v2.0.0-beta7.25380.108 (beta API may change).
  - Nullable reference types should be enabled and warnings addressed.
  - Event-driven networking; SslStream for TLS; UTF-8 with CRLF line endings for IRC.
  - Known issue: EventTcpClient creates and uses a second TcpClient instead of the injected one.
  - No automatic reconnection currently; any reconnection should be optional and conservative.
  - Windows paths and PowerShell are primary for local scripts.
- Process/Quality
  - Avoid real network operations in unit tests; use fakes/mocks and loopback servers for integration.
  - Treat analyzer warnings seriously; aim to keep code warning-free.
  - Secure by default; any insecure modes must be explicit and documented.

## Current State Summary
- EventTcpClient
  - Exposes Connected, Data, Error, Disconnected events.
  - Bug: ConnectAsync creates a new TcpClient (and uses it) instead of the injected client; disposal and cancellation flows are fragile.
  - Uses Task.Run for read loop; cancellation/disposal semantics might cause leaks or double events.
  - TLS path authenticates without detailed options or cancellation-aware overloads.
  - Write is async but without CancellationToken and without serialization of concurrent writes.
- Program
  - CLI via System.CommandLine; default server irc.libera.chat:6697, TLS on by default.
  - Known issues per tasks.md: incorrect PASS command formatting; not respecting TLS flag; slash-command parsing quirks; graceful shutdown not ensured; limited configuration sources.
- Repo/Docs/Tests
  - docs/tasks.md exists; other hygiene items (gitignore, license, CI, tests) pending.

---

## Proposed Improvements by Theme

### 1) Networking and TLS
- Proposed changes
  - Fix EventTcpClient to consistently use and own the injected TcpClient; remove the extra instance (docs/tasks.md #11).
  - Ensure reader/writer derive from the same network stream and are disposed correctly; align leaveOpen semantics (##12, #18).
  - Respect `useTls` flag; use SslClientAuthenticationOptions with SNI and pass CancellationToken to AuthenticateAsClientAsync (##13, #21-#22).
  - Improve cancellation: link tokens, dispose CTS, make read loop exit cleanly and deterministically (##14).
  - Define event semantics and guarantee Disconnected fires exactly once, including for explicit Disconnect and remote close (##15, #19).
  - Add TCP keep-alive and configurable timeouts for connect/read/write (##20).
- Rationale
  - Correctness, resource safety, and predictable lifecycle are foundational for reliability and testability.
  - Security by default requires proper TLS handshake configuration and validation.
- Acceptance criteria
  - No secondary TcpClient instance; all streams come from the owned client.
  - Disconnected fires once per lifecycle; errors are reported only for exceptional conditions.
  - TLS handshake honors `useTls` and uses SNI; cancellation tokens stop operations promptly.

### 2) CLI and User Experience
- Proposed changes
  - Respect --tls/--notls by passing `useTls` to ConnectAsync; fix PASS command formatting; make slash-command parsing robust (##23-#25).
  - Add graceful shutdown on Ctrl+C: await disconnect, flush writes, observe Disconnected (##26).
  - Support environment variables/config file for server, port, nick, user, real, TLS (##27).
- Rationale
  - Correct CLI behaviors reduce runtime surprises and improve usability.
  - Graceful shutdown prevents data loss and improves UX.
- Acceptance criteria
  - Manual tests confirm flags and commands map correctly; Ctrl+C exits cleanly with Disconnected observed.

### 3) IRC Protocol Layer
- Proposed changes
  - Implement minimal IRC message parser/formatter (prefix, command, params, trailing) (##28).
  - Provide handlers/events for common messages (PING/PONG, NOTICE, PRIVMSG, JOIN/PART, ERROR) (##29).
  - Add basic, configurable rate limiting for outgoing messages to avoid flood-kick (##30).
- Rationale
  - Separation of protocol from transport enables testing and reuse; rate limiting protects against server throttling.
- Acceptance criteria
  - Parser round-trips message structures; automated tests cover common commands and PING/PONG behavior.

### 4) Testing Strategy
- Proposed changes
  - Add MSTest project; unit tests for command formatting, slash command mapping, and protocol behaviors (##31).
  - Integration tests with loopback server for non-TLS path; mock TLS by abstracting handshake (##32-#33).
  - Lifecycle tests for event sequencing and cancellation/error paths; concurrency tests for serialized writes and disconnect races (##34-#35).
- Rationale
  - Prevent regressions and document expected behaviors; ensure concurrency safety.
- Acceptance criteria
  - CI runs tests; lifecycle tests pass reliably without flakiness; coverage reported.

### 5) Logging and Observability
- Proposed changes
  - Introduce Microsoft.Extensions.Logging; avoid Console I/O in library code (##46).
  - Add structured logs, redaction for sensitive data (e.g., PASS), and consider basic metrics (##47).
- Rationale
  - Structured logs and metrics simplify troubleshooting while protecting secrets.
- Acceptance criteria
  - Logs are structured and redact secrets; togglable verbosity; no direct Console calls in lower layers.

### 6) Performance and Resource Management
- Proposed changes
  - Make buffer sizes configurable; minimize allocations in read loop; consider System.IO.Pipelines if throughput becomes a goal (##49).
  - Avoid Task.Run for read loop; use a dedicated Task bound to instance lifecycle (##50).
- Rationale
  - Predictable resource usage and potential throughput improvements without premature optimization.
- Acceptance criteria
  - No unnecessary Task.Run overhead; profiles show stable memory behavior under typical load.

### 7) CI, Tooling, and Maintenance
- Proposed changes
  - Add GitHub Actions to build/test and publish coverage (Coverlet + ReportGenerator) (##40).
  - Add Dependabot/Renovate for dependency updates (##41). Add dotnet format and include analyzers in CI quality gates (##42).
- Rationale
  - Maintain code quality and freshness automatically; prevent regressions.
- Acceptance criteria
  - CI runs for PRs; failing tests/analyzers block merges; automated PRs for updates.

### 8) Repository Hygiene
- Proposed changes
  - Add .gitignore; remove committed bin/obj; add LICENSE; expand README; add .editorconfig and consistent style (##1-#8).
- Rationale
  - Clean repository, clear licensing, and consistent style improve contributor experience and reduce noise.
- Acceptance criteria
  - No build artifacts in repo; README explains purpose, build, usage, TLS notes; style enforced.

### 9) Packaging and Documentation
- Proposed changes
  - Provide release packaging instructions and optionally a dotnet tool package (##57).
  - Document connection lifecycle, TLS behavior, error/exit semantics; add CONTRIBUTING and CODE_OF_CONDUCT (##58-#60).
- Rationale
  - Easier distribution and onboarding; clear expectations for contributors and users.
- Acceptance criteria
  - Versioned releases with instructions; docs cover lifecycle and security; contribution docs present.

---

## Roadmap and Milestones
- Phase 1 — Stability and Security (Weeks 1–2)
  - Fix EventTcpClient ownership/streams, TLS respect/options, cancellation, event semantics (#11–#15, #18–#22).
  - PASS/CLI fixes and graceful shutdown (#23–#26). Add .gitignore, LICENSE, README, .editorconfig (#1–#8 subset).
  - Initial tests for lifecycle and basic commands (#31, #34). Introduce logging abstraction (#46).
- Phase 2 — Protocol and Reliability (Weeks 3–4)
  - Implement IRC parser/handlers/rate limiting (#28–#30). Write comprehensive tests (#31–#35).
  - Performance adjustments and read loop refactor (#49–#50). Add keep-alive/timeouts (#20).
  - CI pipeline with coverage and analyzers; Dependabot/Renovate (#40–#42).
- Phase 3 — Observability, Packaging, and Docs (Weeks 5–6)
  - Structured logging, redaction, optional metrics (#47).
  - Packaging and publish instructions; documentation set (README expansion, lifecycle/TLS docs, CONTRIBUTING, CODE_OF_CONDUCT) (#57–#60).

## Risks and Mitigations
- System.CommandLine beta API changes: pin version; review release notes before updates; add wrapper to isolate CLI parsing.
- TLS edge cases and certificate validation: provide opt-in relaxation with warnings; thorough error messages; test common failures.
- Concurrency complexities (write serialization, disconnect races): enforce serialization, comprehensive unit tests, and stress tests.
- Over-scoping: adhere to phased roadmap; deliver stability first.

## Acceptance Criteria (Summary)
- EventTcpClient: single TcpClient, correct TLS handling, clean cancellation, and single Disconnected event.
- CLI: flags respected; PASS/NICK/USER formatted correctly; graceful Ctrl+C.
- Protocol: parser and PING/PONG behavior tested; rate limiting functional.
- Tests/CI: unit and integration tests run in CI with coverage; analyzers enabled; repository clean.
- Logging/Docs: structured logs with redaction; updated README and lifecycle/TLS docs; contribution docs present.

## Open Questions
- Are there specific IRC servers or deployment environments (e.g., self-signed certs) that require relaxed TLS policies by default?
- What are the performance targets (throughput/messages per second) to decide whether to adopt Pipelines now or later?
- Is automatic reconnection desired, and if so, with what policy and user controls?

## Traceability to docs/tasks.md
The proposed improvements reference the numbered items in docs/tasks.md throughout each theme, ensuring alignment and coverage.
