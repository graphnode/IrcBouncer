# IrcBouncer Release Packaging Guide

This document provides comprehensive instructions for creating releases and distributing IrcBouncer.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Dotnet Tool Packaging](#dotnet-tool-packaging)
3. [Traditional Release Builds](#traditional-release-builds)
4. [Release Process](#release-process)
5. [Distribution](#distribution)
6. [Versioning](#versioning)

## Prerequisites

- .NET 9.0 SDK or later
- Git with access to the repository
- NuGet account for publishing (if publishing to nuget.org)
- GitHub CLI (optional, for automated releases)

## Dotnet Tool Packaging

IrcBouncer is configured as a .NET global tool for easy installation and distribution.

### Building the Tool Package

```powershell
# Clean previous builds
dotnet clean --configuration Release

# Build and pack the tool
dotnet pack --configuration Release --output ./nupkg

# The package will be created as ./nupkg/IrcBouncer.{version}.nupkg
```

### Testing the Tool Package Locally

```powershell
# Install from local package
dotnet tool install --global IrcBouncer --add-source ./nupkg

# Test the installation
ircbouncer --help

# Uninstall after testing
dotnet tool uninstall --global IrcBouncer
```

### Publishing to NuGet.org

```powershell
# Push to NuGet (replace {version} with actual version)
dotnet nuget push ./nupkg/IrcBouncer.{version}.nupkg --api-key {your-api-key} --source https://api.nuget.org/v3/index.json

# Users can then install with:
# dotnet tool install --global IrcBouncer
```

## Traditional Release Builds

For users who prefer standalone executables or don't want to use dotnet tools.

### Self-Contained Deployments

Create platform-specific releases that don't require .NET runtime to be installed:

```powershell
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./releases/win-x64

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./releases/linux-x64

# macOS x64
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o ./releases/osx-x64

# macOS ARM64 (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./releases/osx-arm64
```

### Framework-Dependent Deployments

For users who have .NET runtime installed (smaller file sizes):

```powershell
# Cross-platform (requires .NET 9.0 runtime)
dotnet publish -c Release --no-self-contained -p:PublishSingleFile=true -o ./releases/portable

# Platform-specific optimized builds
dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o ./releases/win-x64-fx
dotnet publish -c Release -r linux-x64 --no-self-contained -p:PublishSingleFile=true -o ./releases/linux-x64-fx
dotnet publish -c Release -r osx-x64 --no-self-contained -p:PublishSingleFile=true -o ./releases/osx-x64-fx
```

## Release Process

### 1. Pre-Release Checklist

- [ ] All tests pass locally and in CI
- [ ] Version number updated in `IrcBouncer.csproj`
- [ ] `PackageReleaseNotes` updated with changes
- [ ] README.md reflects current functionality
- [ ] Documentation is up to date

### 2. Create Release Build

```powershell
# Run full test suite
dotnet test --configuration Release

# Create all distribution formats
./scripts/build-release.ps1  # See script below

# Verify packages work correctly
./scripts/test-packages.ps1  # See script below
```

### 3. Tag and Push

```powershell
# Create and push version tag
git tag v1.0.0
git push origin v1.0.0

# Create GitHub release (using GitHub CLI)
gh release create v1.0.0 ./releases/* --title "IrcBouncer v1.0.0" --notes-file CHANGELOG.md
```

## Distribution

### NuGet Package (Recommended)

```powershell
# Users install with:
dotnet tool install --global IrcBouncer

# And use with:
ircbouncer --server irc.libera.chat --nick MyBot
```

### GitHub Releases

Upload the following to GitHub releases:

- `IrcBouncer.{version}.nupkg` - NuGet package
- `ircbouncer-win-x64.zip` - Windows self-contained
- `ircbouncer-linux-x64.tar.gz` - Linux self-contained
- `ircbouncer-osx-x64.tar.gz` - macOS Intel self-contained
- `ircbouncer-osx-arm64.tar.gz` - macOS Apple Silicon self-contained
- `ircbouncer-portable.zip` - Cross-platform framework-dependent

### Package Managers

Future considerations for broader distribution:

- **Chocolatey** (Windows): Create `.nuspec` file
- **Homebrew** (macOS/Linux): Submit formula
- **Scoop** (Windows): Submit manifest
- **Snap** (Linux): Create `snapcraft.yaml`

## Versioning

IrcBouncer follows [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible API changes
- **MINOR** version for backwards-compatible functionality additions
- **PATCH** version for backwards-compatible bug fixes

### Version Update Process

1. Update version in `IrcBouncer.csproj`:
   ```xml
   <Version>1.2.3</Version>
   ```

2. Update `PackageReleaseNotes` with changes

3. Update any version references in documentation

## Automation Scripts

### build-release.ps1

```powershell
#!/usr/bin/env pwsh
# Build all release artifacts

param(
    [string]$Version = "1.0.0"
)

Write-Host "Building IrcBouncer v$Version release artifacts..."

# Clean
dotnet clean -c Release

# Create output directories
New-Item -ItemType Directory -Force -Path "./releases"
New-Item -ItemType Directory -Force -Path "./nupkg"

# Build NuGet package
dotnet pack -c Release -o ./nupkg

# Build self-contained releases
$runtimes = @("win-x64", "linux-x64", "osx-x64", "osx-arm64")

foreach ($runtime in $runtimes) {
    Write-Host "Building $runtime..."
    dotnet publish -c Release -r $runtime --self-contained true -p:PublishSingleFile=true -o "./releases/$runtime"
    
    if ($runtime.StartsWith("win")) {
        Compress-Archive -Path "./releases/$runtime/*" -DestinationPath "./releases/ircbouncer-$runtime.zip" -Force
    } else {
        tar -czf "./releases/ircbouncer-$runtime.tar.gz" -C "./releases/$runtime" .
    }
}

# Build portable version
dotnet publish -c Release --no-self-contained -p:PublishSingleFile=true -o "./releases/portable"
Compress-Archive -Path "./releases/portable/*" -DestinationPath "./releases/ircbouncer-portable.zip" -Force

Write-Host "Release build complete!"
```

### test-packages.ps1

```powershell
#!/usr/bin/env pwsh
# Test release packages

Write-Host "Testing release packages..."

# Test NuGet package locally
try {
    dotnet tool install --global IrcBouncer --add-source ./nupkg
    $testResult = ircbouncer --help
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ NuGet package test passed"
    } else {
        Write-Host "❌ NuGet package test failed"
    }
    dotnet tool uninstall --global IrcBouncer
} catch {
    Write-Host "❌ NuGet package test failed: $_"
}

# Test platform executables
$platforms = @("win-x64", "linux-x64", "osx-x64", "osx-arm64", "portable")

foreach ($platform in $platforms) {
    if (Test-Path "./releases/$platform") {
        Write-Host "✅ $platform build exists"
    } else {
        Write-Host "❌ $platform build missing"
    }
}

Write-Host "Package testing complete!"
```

## Security Considerations

- Sign releases with a code signing certificate when possible
- Use secure channels (HTTPS) for distribution
- Provide checksums for downloadable files
- Keep private keys and API tokens secure
- Regularly update dependencies to address security vulnerabilities

## Support

For packaging-related questions or issues:

1. Check existing [GitHub Issues](https://github.com/yourusername/IrcBouncer/issues)
2. Create a new issue with the `packaging` label
3. Include details about your build environment and target platform
