# Coralph

A first cut of a “Ralph loop” runner implemented in C#/.NET 10 using the GitHub Copilot SDK.

## Installation

### Option 1: Download Pre-built Binary (Recommended)

Download the latest release for your platform from the [Releases page](https://github.com/dariuszparys/coralph/releases):

- **Windows**: `coralph-win-x64.exe`
- **macOS (Intel)**: `coralph-osx-x64`
- **macOS (Apple Silicon)**: `coralph-osx-arm64`
- **Linux**: `coralph-linux-x64`

After downloading:
- **macOS/Linux**: Make the binary executable: `chmod +x coralph-*`
- **Windows**: Run directly or add to your PATH

### Option 2: Build from Source

**Prerequisites:**
- .NET SDK 10 preview
- GitHub CLI (`gh`) authenticated if you use `--refresh-issues`

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

# customize streaming output
dotnet run --project src/Coralph -- --max-iterations 5 --show-reasoning false
```

## Documentation

- **[Using Coralph with Other Repositories](docs/using-with-other-repos.md)** - Adapt Coralph for Python, JavaScript, Go, and other tech stacks

## Features

### Streaming Output Improvements
- **Visual styling**: Color-coded output for reasoning (cyan), assistant text (green), and tool execution (yellow)
- **Configuration**: Control display with `--show-reasoning` and `--colorized-output` flags
- **Mode tracking**: Automatic separation of reasoning vs. assistant vs. tool output

### Custom Tools
Built-in domain-specific tools available to the assistant:
- `list_open_issues`: Query issues from issues.json
- `get_progress_summary`: Retrieve recent progress entries
- `search_progress`: Search progress.txt for specific terms
```

## Build a distributable binary

Coralph is configured for self-contained, single-file publishing by default. Choose your target platform's Runtime Identifier (RID):

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

**Note**: Self-contained builds include the .NET runtime (~77MB), so users don't need .NET installed.

## Development

### Using the Justfile

Coralph uses [just](https://just.systems) as a cross-platform command runner with PowerShell support.

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

**Note**: Install `just` from [https://just.systems](https://just.systems). The justfile uses PowerShell for cross-platform compatibility.

## Versioning and Releases

Coralph uses [Semantic Versioning](https://semver.org/) (SemVer).

### How versioning works

- **Development builds**: Default to `0.0.1-dev` when building locally
- **Release builds**: Version is automatically extracted from git tags (e.g., `v1.2.3` → `1.2.3`)

### Creating a release

1. **Tag the release** with a semantic version:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **GitHub Actions automatically**:
   - Builds self-contained binaries for all platforms
   - Creates a GitHub Release with the version from the tag
   - Attaches platform-specific binaries to the release

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

The loop stops early when the assistant outputs a line containing `COMPLETE`, or when issues.json has no open issues (prints `NO_OPEN_ISSUES`).
