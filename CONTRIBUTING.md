# Contributing to IrcBouncer

We welcome contributions to IrcBouncer! This document provides guidelines for contributing to the project.

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Environment](#development-environment)
4. [Coding Standards](#coding-standards)
5. [Testing Requirements](#testing-requirements)
6. [Contribution Workflow](#contribution-workflow)
7. [Pull Request Guidelines](#pull-request-guidelines)
8. [Issue Guidelines](#issue-guidelines)
9. [Release Process](#release-process)

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Git
- A code editor (Visual Studio, VS Code, Rider, etc.)
- Basic familiarity with C#, async/await, and IRC protocol

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/yourusername/IrcBouncer.git
   cd IrcBouncer
   ```
3. Add the upstream remote:
   ```bash
   git remote add upstream https://github.com/originalowner/IrcBouncer.git
   ```

## Development Environment

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project IrcBouncer -- --help
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test IrcBouncer.Tests
```

### IDE Configuration

#### Visual Studio / Rider
- The solution file `IrcBouncer.sln` contains all projects
- EditorConfig settings will be automatically applied
- Enable nullable reference type warnings

#### VS Code
- Install the C# extension
- Use the integrated terminal for dotnet commands
- Configure the C# extension to use the project's EditorConfig

## Coding Standards

### General Guidelines

- Follow established C# conventions and .NET design guidelines
- Use meaningful names for classes, methods, and variables
- Write self-documenting code with clear intent
- Add XML documentation for public APIs
- Keep methods focused and reasonably sized
- Prefer composition over inheritance where appropriate

### Specific Standards

#### Nullable Reference Types
```csharp
// Enable nullable reference types (already configured)
#nullable enable

// Use nullable annotations appropriately
public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
{
    // Implementation
}
```

#### Async/Await Patterns
```csharp
// Use ConfigureAwait(false) in library code
await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

// Prefer async methods over sync when available
await writer.WriteLineAsync(message, cancellationToken).ConfigureAwait(false);

// Always accept CancellationToken for async operations
public async Task SendAsync(string message, CancellationToken cancellationToken = default)
```

#### Error Handling
```csharp
// Use specific exceptions where appropriate
throw new ArgumentNullException(nameof(parameter));

// Log errors with structured logging
logger.LogError(exception, "Failed to connect to {Server}:{Port}", server, port);

// Don't catch and rethrow without adding value
// Bad:
try { ... } catch (Exception ex) { throw; }

// Good:
try { ... } catch (Exception ex) { logger.LogError(ex, "Context"); throw; }
```

#### Resource Management
```csharp
// Implement IDisposable properly
public void Dispose()
{
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
}

protected virtual void Dispose(bool disposing)
{
    if (disposing && !_disposed)
    {
        // Dispose managed resources
        _resource?.Dispose();
        _disposed = true;
    }
}
```

### Code Formatting

The project uses EditorConfig for consistent formatting:
- 4 spaces for indentation
- UTF-8 encoding without BOM
- Trim trailing whitespace
- Insert final newline

Run `dotnet format` before committing to ensure consistent formatting.

## Testing Requirements

### Test Coverage

- Aim for high test coverage on core functionality
- All public APIs should have corresponding tests
- Focus on edge cases and error conditions
- Test concurrent scenarios where applicable

### Test Categories

#### Unit Tests
```csharp
[TestMethod]
public async Task SendAsync_ValidMessage_SendsToServer()
{
    // Arrange
    var client = new IrcClient();
    var mockConnection = new MockConnection();
    
    // Act
    await client.SendAsync("PRIVMSG #test :Hello");
    
    // Assert
    Assert.AreEqual("PRIVMSG #test :Hello", mockConnection.LastMessage);
}
```

#### Integration Tests
```csharp
[TestMethod]
public async Task ConnectAsync_ValidServer_EstablishesConnection()
{
    // Use loopback server for integration tests
    using var server = new LoopbackIrcServer();
    await server.StartAsync();
    
    using var client = new EventTcpClient();
    await client.ConnectAsync("127.0.0.1", server.Port, useTls: false);
    
    // Verify connection established
}
```

### Test Guidelines

- Use descriptive test names that explain the scenario
- Follow Arrange-Act-Assert pattern
- Make tests deterministic and independent
- Avoid real network calls in unit tests
- Use mocks/fakes for external dependencies
- Test both success and failure paths

## Contribution Workflow

### Branch Strategy

1. **Create a feature branch:**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes:**
   - Write code following the coding standards
   - Add or update tests as needed
   - Update documentation if applicable

3. **Commit your changes:**
   ```bash
   git add .
   git commit -m "Add feature: brief description of changes"
   ```

4. **Keep your branch updated:**
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

5. **Push your branch:**
   ```bash
   git push origin feature/your-feature-name
   ```

### Commit Messages

Use clear, descriptive commit messages:

```
Add rate limiting for outbound IRC messages

- Implement configurable rate limiter in IrcClient
- Add tests for rate limiting behavior
- Update documentation with rate limiting options

Fixes #123
```

**Format:**
- Use imperative mood ("Add", "Fix", "Update")
- First line should be 50 characters or less
- Include more details in the body if needed
- Reference issues with "Fixes #123" or "Closes #123"

## Pull Request Guidelines

### Before Submitting

- [ ] All tests pass locally
- [ ] Code follows project coding standards
- [ ] New code is covered by tests
- [ ] Documentation is updated if needed
- [ ] Branch is up-to-date with main
- [ ] No merge commits (use rebase)

### PR Description Template

```markdown
## Description
Brief description of changes and motivation.

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed

## Checklist
- [ ] Code follows the project's coding standards
- [ ] Self-review of the code completed
- [ ] Code is commented, particularly in hard-to-understand areas
- [ ] Corresponding changes to the documentation made
- [ ] No new warnings introduced

## Related Issues
Fixes #(issue number)
```

### Review Process

1. **Automated Checks:** CI pipeline runs tests and quality checks
2. **Code Review:** Maintainers review the code for:
   - Correctness and functionality
   - Code quality and maintainability
   - Test coverage and quality
   - Documentation completeness
3. **Feedback:** Address any requested changes
4. **Approval:** Once approved, the PR will be merged

## Issue Guidelines

### Before Creating an Issue

- Search existing issues to avoid duplicates
- Check if the issue is already fixed in the latest version
- Gather all relevant information

### Issue Types

#### Bug Reports
Include:
- Clear description of the problem
- Steps to reproduce
- Expected vs actual behavior
- Environment details (OS, .NET version)
- Stack traces or error messages
- Minimal code sample if applicable

#### Feature Requests
Include:
- Clear description of the feature
- Use cases and motivation
- Proposed implementation approach (if any)
- Potential impact on existing functionality

#### Documentation Issues
Include:
- Which documentation needs improvement
- Specific sections that are unclear
- Suggestions for improvement

### Labels

Issues are labeled to help with organization:
- `bug`: Something isn't working
- `enhancement`: New feature or request
- `documentation`: Improvements to documentation
- `good first issue`: Good for newcomers
- `help wanted`: Extra attention needed
- `performance`: Performance-related issues
- `security`: Security-related issues

## Release Process

### Versioning

IrcBouncer follows [Semantic Versioning](https://semver.org/):
- `MAJOR.MINOR.PATCH`
- Major: Breaking changes
- Minor: New features (backward compatible)
- Patch: Bug fixes (backward compatible)

### Release Checklist

- [ ] All tests pass
- [ ] Documentation updated
- [ ] Version number updated
- [ ] Release notes prepared
- [ ] Package builds successfully
- [ ] Integration tests pass

## Getting Help

### Communication Channels

- **GitHub Issues**: For bug reports and feature requests
- **GitHub Discussions**: For questions and general discussion
- **Code Reviews**: For detailed technical discussion

### Documentation

- **README.md**: Basic usage and setup
- **docs/ARCHITECTURE.md**: Technical architecture details
- **docs/PACKAGING.md**: Release and packaging information
- **API Documentation**: XML docs in source code

## Recognition

Contributors will be recognized in the following ways:
- Listed in the project's contributors
- Mentioned in release notes for significant contributions
- Invited to join the maintainers team for sustained contributions

Thank you for contributing to IrcBouncer! 🎉
