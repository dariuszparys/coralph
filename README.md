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

Coralph now defaults unattended loop iterations to isolated Docker containers. Opt back into host execution only when you explicitly trust the local run environment.

```bash
./coralph --max-iterations 5
./coralph --docker-image ghcr.io/devcontainers/dotnet:10.0
./coralph --docker-sandbox false   # Opt back into host execution
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

By default Coralph uses a **deny-dangerous-tools** posture: shell execution and file-write style tools are blocked unless you explicitly allow them.

Use `--tool-allow` to opt specific dangerous tools back in, or to create a full explicit allowlist. Use `--tool-deny` to add stricter blocks on top; deny rules still win.

```bash
./coralph --tool-allow bash,edit*         # Opt shell and write tools back in explicitly
./coralph --tool-deny bash,read_file      # Add stricter blocks on top of the defaults
./coralph --tool-allow list_open_issues   # Allow only listed tools; everything else denied
```

Deny takes precedence over allow when both lists are non-empty.

### Client Name

Set the client name sent to the Copilot session (defaults to `coralph`):
```bash
./coralph --client-name my-automation
```

This value is passed to `SessionConfig.ClientName` and can also be set via the config file.

### Reasoning Effort

Control the model's reasoning budget (low, medium, or high). Omit to use the model's default:
```bash
./coralph --reasoning-effort high
```

Passed as `SessionConfig.ReasoningEffort`. Not all models respect this setting.

### Hooks and User-Input Handlers

The Copilot SDK provides `OnPreToolUse`, `OnPostToolUse`, and `OnUserInputRequest` callbacks on `SessionConfig`. Coralph intentionally does not implement them:

- **Tool-use hooks** are unnecessary because `CopilotSessionEventRouter` already captures all tool events via `session.On()`.
- **User-input handlers** are inappropriate for an unattended loop; models should not prompt for interactive input during an automated run.

### OpenAI-Compatible Providers

Use an OpenAI-compatible provider with optional base URL and wire API overrides:
```bash
./coralph --provider-type openai --provider-api-key sk-your-key \
	--provider-base-url https://api.your-provider.example/v1 \
	--provider-wire-api openai
```

### OpenRouter

[OpenRouter](https://openrouter.ai) is supported as a first-class provider type. The base URL (`https://openrouter.ai/api/v1`) is set automatically — no need to pass `--provider-base-url`.

```bash
# Minimal usage — model defaults to whatever the Copilot SDK selects
./coralph --provider-type openrouter --provider-api-key sk-or-xxxxx

# Select a specific model via --provider-wire-api
./coralph --provider-type openrouter --provider-api-key sk-or-xxxxx \
    --provider-wire-api anthropic/claude-3.5-sonnet
```

Or via `coralph.config.json`:
```json
{
  "LoopOptions": {
    "ProviderType": "openrouter",
    "ProviderApiKey": "sk-or-xxxxx",
    "ProviderWireApi": "anthropic/claude-3.5-sonnet"
  }
}
```

Do not commit API keys to source control. Prefer environment variables such as `OPENROUTER_API_KEY` or `CORALPH_PROVIDER_API_KEY` over `--provider-api-key`, and use secrets or local configuration for overrides when needed.

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

The loop stops when the AI outputs `COMPLETE`, `NO_OPEN_ISSUES`, or `ALL_TASKS_COMPLETE`.

---

## Extending Tech Stack Support

1. Create `examples/<stack>-prompt.md` with build/test commands
2. Update `--init` detection logic for your manifest files (see src/Coralph/InitWorkflow.cs)
3. Submit a PR – see [CONTRIBUTING.md](CONTRIBUTING.md)
