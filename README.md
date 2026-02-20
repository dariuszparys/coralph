# Coralph

An AI-powered "Ralph loop" runner for automated development workflows using the GitHub Copilot SDK.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10-blue.svg)](https://dotnet.microsoft.com/)
[![GitHub release](https://img.shields.io/github/v/release/dariuszparys/coralph)](https://github.com/dariuszparys/coralph/releases)

## What is Coralph?

Coralph automates routine coding tasks by running an AI assistant in a loop that:
1. Reads open GitHub issues → 2. Breaks them into tasks → 3. Implements changes → 4. Runs tests → 5. Commits code → Repeat

---

## For New Users

### Installation

**Download pre-built binary** (recommended):

| Platform | Binary |
|----------|--------|
| Windows | [`Coralph-win-x64.exe`](https://github.com/dariuszparys/coralph/releases) |
| macOS Intel | [`Coralph-osx-x64`](https://github.com/dariuszparys/coralph/releases) |
| macOS ARM | [`Coralph-osx-arm64`](https://github.com/dariuszparys/coralph/releases) |
| Linux | [`Coralph-linux-x64`](https://github.com/dariuszparys/coralph/releases) |

```bash
# macOS/Linux: Make executable
chmod +x Coralph-*
```

**Or build from source** (requires .NET SDK 10):
```bash
dotnet publish src/Coralph -c Release -r linux-x64 --self-contained
```

**Install as a .NET tool** (requires .NET SDK 10):
```bash
dotnet tool install -g Coralph
coralph --version
```

If you use `dnx`, you can run directly from NuGet without installing:
```bash
dnx Coralph -- --version
```

### Quick Start

```bash
# 1. Navigate to your repository
cd your-repo

# 2. Initialize (auto-detects tech stack)
./coralph --init

# 3. (Optional) Fetch GitHub issues
./coralph --refresh-issues --repo owner/repo-name

# 4. Run the loop
./coralph --max-iterations 10

# Alternative: run from anywhere by targeting a repository path
./coralph --working-dir /path/to/your-repo --max-iterations 10
```

The init command creates `issues.json`, `progress.txt`, `coralph.config.json`, and a `prompt.md` template for your tech stack (Python, JavaScript, Go, Rust, or .NET) using only the Coralph binary.

### Common Commands

```bash
./coralph --max-iterations 10                    # Run 10 iterations
./coralph --refresh-issues --repo owner/name     # Fetch GitHub issues
./coralph --init                                 # Initialize repository artifacts
./coralph --demo                                 # Launch demo mode with mock UI data
./coralph --working-dir /path/to/repo --init     # Target a repo without cd
./coralph --version                              # Show version
./coralph --help                                 # Show all options
```

### More Documentation

- **[Architecture](docs/architecture.md)** – Component diagrams and data flow
- **[Using with Other Repos](docs/using-with-other-repos.md)** – Adapt for Python, JS, Go, Rust
- **[Changelog](CHANGELOG.md)** – Release history

---

## For Contributors

### Development Setup

```bash
git clone https://github.com/dariuszparys/coralph.git && cd coralph
dotnet restore && dotnet build && dotnet test
```

### Key Commands

```bash
just ci          # Full CI pipeline (restore, build, test)
just build       # Build solution
just test        # Run tests
just changelog unreleased  # Refresh Unreleased section from latest commits
just changelog v1.0.0  # Generate changelog
just tag v1.0.0        # Create release tag
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for workflow details and commit conventions.

### Pre-commit Hook (Optional)

```bash
git config core.hooksPath .githooks  # Enable formatting checks
```

### Creating Releases

```bash
just changelog v1.0.0      # Generate changelog from commits
git add CHANGELOG.md && git commit -m "docs: changelog v1.0.0" && git push
just tag v1.0.0            # Create and push tag (triggers release)
```

After a published release, GitHub Actions now bumps `src/Coralph/Coralph.csproj` to the next `-dev` patch version automatically.

To bump development version manually (patch/minor/major):

```bash
just bump-dev patch
just bump-dev minor
just bump-dev major
```

---

## Advanced Features

### Docker Sandbox

Run iterations in isolated containers:
```bash
./coralph --max-iterations 5 --docker-sandbox true
./coralph --docker-sandbox true --docker-image ghcr.io/devcontainers/dotnet:10.0
```

### Azure Boards Integration

Fetch work items from Azure DevOps:
```bash
./coralph --refresh-issues-azdo --azdo-organization https://dev.azure.com/myorg --azdo-project MyProject
```

### Structured Event Stream

Emit JSONL for integrations:
```bash
./coralph --stream-events true 1>events.jsonl 2>console.log
```

When `--stream-events true` is enabled, Coralph forces classic console output to preserve machine-readable JSONL on stdout.

### UI Modes

Coralph defaults to an interactive TUI in terminals, with automatic fallback to classic output when input/output is redirected.

```bash
./coralph --ui auto      # Default: TUI for interactive terminals, classic otherwise
./coralph --ui tui       # Force TUI
./coralph --ui classic   # Force classic console output
./coralph --demo         # Demo mode with mock data (no backend writes)
```

### Tool Permissions

Control AI tool access:
```bash
./coralph --tool-deny bash,read_file      # Block specific tools
./coralph --tool-allow list_open_issues   # Allow only listed tools
```

### OpenAI-Compatible Providers

Use an OpenAI-compatible provider with optional base URL and wire API overrides:
```bash
./coralph --provider-type openai --provider-api-key sk-your-key \
	--provider-base-url https://api.your-provider.example/v1 \
	--provider-wire-api openai
```

Do not commit API keys to source control. Use secrets or local configuration for `--provider-api-key` or config overrides.

See `./coralph --help` for all options.

---

## How It Works

| File | Purpose |
|------|---------|
| `prompt.md` | AI instructions for your codebase |
| `issues.json` | Cached GitHub issues |
| `generated_tasks.json` | Task backlog from issues |
| `progress.txt` | Completed work log |
| `coralph.config.json` | Configuration overrides |

The loop stops when the AI outputs `COMPLETE` or no open issues remain.

---

## Extending Tech Stack Support

1. Create `examples/<stack>-prompt.md` with build/test commands
2. Update `--init` detection logic for your manifest files (see src/Coralph/Program.cs)
3. Submit a PR – see [CONTRIBUTING.md](CONTRIBUTING.md)
