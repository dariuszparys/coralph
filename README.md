# Coralph

A first cut of a “Ralph loop” runner implemented in C#/.NET 10 using the GitHub
Copilot SDK.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10-blue.svg)](https://dotnet.microsoft.com/)
[![GitHub release](https://img.shields.io/github/v/release/dariuszparys/coralph)](https://github.com/dariuszparys/coralph/releases)

## What is a Ralph Loop?

A Ralph loop is an AI-powered development workflow where an AI assistant:

1. Reads open GitHub issues from your repository
2. Breaks them down into small, manageable tasks
3. Implements changes incrementally
4. Runs tests and commits code automatically
5. Repeats until all issues are resolved

Coralph automates this process, allowing you to delegate routine coding tasks to
AI while maintaining quality through automated testing and feedback loops.

## Table of Contents

- [What is a Ralph Loop?](#what-is-a-ralph-loop)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Run](#run)
- [Documentation](#documentation)
- [Features](#features)
- [How It Works](#how-it-works)
- [Build a distributable binary](#build-a-distributable-binary)
- [Development](#development)
- [Versioning and Releases](#versioning-and-releases)

## Installation

### Option 1: Download Pre-built Binary (Recommended)

Download the latest release for your platform from the
[Releases page](https://github.com/dariuszparys/coralph/releases):

- **Windows**: `Coralph-win-x64.exe`
- **macOS (Intel)**: `Coralph-osx-x64`
- **macOS (Apple Silicon)**: `Coralph-osx-arm64`
- **Linux**: `Coralph-linux-x64`

> **Note**: Release binaries use capitalized names (e.g., `Coralph-linux-x64`),
> while examples in this documentation use lowercase for convenience (e.g.,
> `coralph`). Rename the binary as needed.

After downloading:

- **macOS/Linux**: Make the binary executable: `chmod +x coralph-*`
- **Windows**: Run directly or add to your PATH

### Option 2: Build from Source

**Prerequisites:**

- .NET SDK 10 preview
- GitHub CLI (`gh`) authenticated if you use `--refresh-issues`

## Quick Start

```bash
# 1. Download and install (or build from source above)
chmod +x coralph-linux-x64
sudo mv coralph-linux-x64 /usr/local/bin/coralph

# 2. Navigate to your repository
cd your-repo

# 3. Fetch your GitHub issues
coralph --refresh-issues --repo owner/repo-name

# 4. Run the loop
coralph --max-iterations 10
```

## Run

```bash
# If using pre-built binary, replace 'dotnet run --project src/Coralph --' with './coralph-<platform>'
# For example: ./coralph-linux-x64 --max-iterations 10

# optional: refresh issues.json using gh
(dotnet run --project src/Coralph -- --refresh-issues --repo owner/name) || true

# run loop (default reads ./issues.json and uses ./coralph.config.json if present)
dotnet run --project src/Coralph -- --max-iterations 10

# create a config file with defaults (safe: refuses to overwrite existing file)
dotnet run --project src/Coralph -- --initial-config

# run loop using a config file (CLI flags override config values)
dotnet run --project src/Coralph -- --config coralph.config.json --max-iterations 5

# run loop using the bundled sample harness (no GitHub access needed)
dotnet run --project src/Coralph -- --issues-file issues.sample.json --max-iterations 10

# show version
dotnet run --project src/Coralph -- --version

# customize streaming output
dotnet run --project src/Coralph -- --max-iterations 5 --show-reasoning false
```

## Documentation

- **[Architecture](docs/architecture.md)** - High-level architecture diagrams
  and component descriptions
- **[Using Coralph with Other Repositories](docs/using-with-other-repos.md)** -
  Adapt Coralph for Python, JavaScript, Go, and other tech stacks

## Features

### Streaming Output Improvements

- **Visual styling**: Color-coded output for reasoning (cyan), assistant text
  (green), and tool execution (yellow)
- **Configuration**: Control display with `--show-reasoning` and
  `--colorized-output` flags
- **Mode tracking**: Automatic separation of reasoning vs. assistant vs. tool
  output

### Custom Tools

Built-in domain-specific tools available to the assistant:

- `list_open_issues`: Query issues from issues.json
- `get_progress_summary`: Retrieve recent progress entries
- `search_progress`: Search progress.txt for specific terms

## How It Works

Coralph uses several files in your repository to manage the development loop:

- **`prompt.md`**: Instructions for the AI assistant on how to work with your
  codebase
- **`issues.json`**: Cached GitHub issues (refreshed via `--refresh-issues`)
- **`progress.txt`**: Append-only log of completed work and learnings
- **`coralph.config.json`**: Optional configuration overrides

The loop stops early when:

- The assistant outputs a line containing `COMPLETE`, or
- `issues.json` has no open issues (prints `NO_OPEN_ISSUES`)

## Build a distributable binary

Coralph is configured for self-contained, single-file publishing by default.
Choose your target platform's Runtime Identifier (RID):

```bash
# Linux (most common)
dotnet publish src/Coralph -c Release -r linux-x64 --self-contained

# macOS (Intel)
dotnet publish src/Coralph -c Release -r osx-x64 --self-contained

# macOS (Apple Silicon)
dotnet publish src/Coralph -c Release -r osx-arm64 --self-contained

# Windows
dotnet publish src/Coralph -c Release -r win-x64 --self-contained

# The binary will be in: src/Coralph/bin/Release/net10.0/<RID>/publish/
```

**Note**: Self-contained builds include the .NET runtime (~77MB), so users don't
need .NET installed.

## Development

### Using the Justfile

Coralph uses [just](https://just.systems) as a cross-platform command runner
with PowerShell support.

```bash
# List available recipes
just

# Run full CI pipeline (restore, build, test)
just ci

# Individual steps
just restore   # Restore dependencies
just build     # Build solution
just test      # Run tests

# Create a version tag
just tag v1.0.0
```

**Note**: Install `just` from [https://just.systems](https://just.systems). The
justfile uses PowerShell for cross-platform compatibility.

## Versioning and Releases

Coralph uses [Semantic Versioning](https://semver.org/) (SemVer).

### How versioning works

- **Development builds**: Default to `0.0.1-dev` when building locally without
  explicit version
- **Release builds**: Version is automatically extracted from git tags (e.g.,
  `v1.2.3` → `1.2.3`) via GitHub Actions

### Local builds with custom version

Local builds use the default `0.0.1-dev` version unless you override it. To
simulate a release build locally:

```bash
# Override version with any semver value
dotnet publish src/Coralph -c Release -r osx-arm64 --self-contained /p:Version=1.2.3

# Use justfile to auto-detect version from latest git tag
just publish-local osx-arm64
```

### Creating a release

1. **Tag the release** with a semantic version:

   ```bash
   just tag v1.0.0
   git push origin v1.0.0
   ```

2. **GitHub Actions automatically**:
   - Builds self-contained binaries for all platforms
   - Creates a GitHub Release with the version from the tag
   - Attaches platform-specific binaries to the release
   - **Generates release notes** from commits and PRs since the last release

### Version in code

The version is embedded in the assembly and can be accessed at runtime:

```csharp
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion ?? "unknown";
```

Files used:

- `prompt.md` (instructions)
- `issues.json` (input; optional refresh via `gh`)
- `progress.txt` (append-only log)
- `coralph.config.json` (optional configuration overrides)

The loop stops early when the assistant outputs a line containing `COMPLETE`, or
when issues.json has no open issues (prints `NO_OPEN_ISSUES`).

## Documentation Validation

The CI pipeline automatically validates that `.github/copilot-instructions.md`
stays in sync with the codebase:

- **Required sections**: Repo context, Build and test, Run loops
- **File references**: Validates that referenced files (Coralph.sln,
  src/Coralph, prompt.md, progress.txt) exist

Run the validation locally:

```bash
.github/scripts/validate-copilot-instructions.sh
```

When updating documentation, ensure the validation script passes before
submitting a PR.
