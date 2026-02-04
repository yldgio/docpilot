# Installation Guide

This guide covers all installation methods for DocPilot.

## System Requirements

| Requirement | Version |
|-------------|---------|
| Operating System | Windows 10+, Linux (Ubuntu 20.04+), macOS 12+ |
| GitHub Copilot CLI | Latest |
| Git | 2.30+ |

## Installation Methods

### Method 1: Pre-built Binaries (Recommended)

Download the self-contained executable for your platform. No runtime dependencies required.

#### Windows

```powershell
# Download
Invoke-WebRequest -Uri "https://github.com/yldgio/docpilot/releases/latest/download/docpilot-win-x64.exe" -OutFile "docpilot.exe"

# Option A: Run from current directory
.\docpilot.exe --help

# Option B: Add to PATH (run as Administrator)
Move-Item docpilot.exe "C:\Program Files\DocPilot\docpilot.exe"
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\Program Files\DocPilot", "Machine")
```

#### Linux

```bash
# Download
curl -L https://github.com/yldgio/docpilot/releases/latest/download/docpilot-linux-x64 -o docpilot

# Make executable
chmod +x docpilot

# Option A: Run from current directory
./docpilot --help

# Option B: Install system-wide
sudo mv docpilot /usr/local/bin/
docpilot --help
```

#### macOS

```bash
# Download
curl -L https://github.com/yldgio/docpilot/releases/latest/download/docpilot-osx-x64 -o docpilot

# Make executable
chmod +x docpilot

# Remove quarantine (first run may require this)
xattr -d com.apple.quarantine docpilot

# Option A: Run from current directory
./docpilot --help

# Option B: Install to PATH
sudo mv docpilot /usr/local/bin/
docpilot --help
```

### Method 2: Build from Source

Requires .NET 10.0 SDK.

```bash
# Clone repository
git clone https://github.com/yldgio/docpilot.git
cd docpilot

# Build
dotnet build -c Release

# Run
dotnet run --project src/DocPilot -- --help

# Or create a single-file executable
dotnet publish src/DocPilot -c Release -r linux-x64 --self-contained -o ./publish
./publish/docpilot --help
```

### Method 3: .NET Global Tool

```bash
# Pack the tool
dotnet pack src/DocPilot -c Release

# Install globally
dotnet tool install -g --add-source src/DocPilot/nupkg DocPilot

# Run
docpilot --help

# Uninstall
dotnet tool uninstall -g DocPilot
```

## Prerequisites Setup

### GitHub Copilot CLI

DocPilot requires GitHub Copilot CLI to be installed and authenticated.

```bash
# Install as GitHub CLI extension
gh extension install github/gh-copilot

# Authenticate with GitHub
gh auth login

# Verify installation
gh copilot --version
```

### GitHub Token (for PR creation)

The `docpilot pr` command requires a GitHub token with appropriate permissions.

#### Option A: Environment Variable

```bash
# Linux/macOS
export GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxx"

# Windows PowerShell
$env:GITHUB_TOKEN = "ghp_xxxxxxxxxxxxxxxxxxxx"
```

#### Option B: GitHub CLI Authentication

If you've authenticated with `gh auth login`, DocPilot can use the same credentials.

#### Required Token Scopes

| Scope | Required For |
|-------|--------------|
| `repo` | Reading repository content, creating branches |
| `pull_request` | Creating and managing pull requests |

## Verifying Installation

Run the following commands to verify your installation:

```bash
# Check DocPilot version
docpilot --help

# Verify GitHub Copilot CLI
gh copilot --version

# Test in a git repository
cd /path/to/your/repo
docpilot analyze --staged
```

## Troubleshooting

### "copilot: command not found"

GitHub Copilot CLI is not installed or not in PATH.

```bash
# Install GitHub Copilot CLI
gh extension install github/gh-copilot
```

### "Permission denied" on Linux/macOS

```bash
chmod +x docpilot
```

### macOS: "cannot be opened because the developer cannot be verified"

```bash
xattr -d com.apple.quarantine docpilot
```

### .NET runtime errors (source build only)

Ensure you have .NET 10.0 SDK installed:

```bash
dotnet --version  # Should show 10.0.x
```

## Updating

### Pre-built Binaries

Download the latest version from [GitHub Releases](https://github.com/yldgio/docpilot/releases) and replace the existing executable.

### Source Build

```bash
cd docpilot
git pull origin main
dotnet build -c Release
```

### .NET Global Tool

```bash
dotnet tool update -g DocPilot --add-source src/DocPilot/nupkg
```

## Next Steps

- [Quick Start Guide](getting-started.md) — Your first DocPilot run
- [Configuration Reference](configuration.md) — Customize DocPilot behavior
- [GitHub Actions Setup](github-actions.md) — Automate documentation updates
