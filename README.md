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

# list available Copilot models and exit
dotnet run --project src/Coralph -- --list-models

# list available Copilot models as JSON and exit
dotnet run --project src/Coralph -- --list-models-json

# customize streaming output
dotnet run --project src/Coralph -- --max-iterations 5 --show-reasoning false

# emit structured JSON events (stdout) and keep human output on stderr
dotnet run --project src/Coralph -- --max-iterations 5 --stream-events true 1>events.jsonl 2>coralph.log

# restrict tool permissions (deny takes precedence)
dotnet run --project src/Coralph -- --max-iterations 5 --tool-deny bash,read_file

# allow only specific tool/permission kinds (everything else denied)
dotnet run --project src/Coralph -- --max-iterations 5 --tool-allow list_open_issues,search_progress

# run loop inside Docker for isolation (requires Docker)
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true

# override Docker image for sandbox runs
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true --docker-image ghcr.io/devcontainers/dotnet:10.0

# note: Docker sandbox requires the GitHub Copilot CLI inside the image
# (Node.js 24+ and `npm i -g @github/copilot`), or pass --cli-path/--cli-url.

# reuse host Copilot CLI auth inside the sandbox
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true --copilot-config-path ~/.copilot

# non-interactive auth (recommended for CI / headless)
# requires a fine-grained PAT with "Copilot Requests" permission (classic PATs won't work)
# create one at:
# https://github.com/settings/personal-access-tokens
export GH_TOKEN=ghp_your_fine_grained_pat
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true --docker-image coralph-dotnet-copilot:10.0

# or pass the token explicitly (sets GH_TOKEN inside the sandbox)
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true --docker-image coralph-dotnet-copilot:10.0 --copilot-token ghp_your_fine_grained_pat

# build a custom image with Copilot CLI preinstalled
docker build -f Dockerfile.copilot -t coralph-dotnet-copilot:10.0 .

# use the custom image for sandbox runs
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true --docker-image coralph-dotnet-copilot:10.0

# or set it once in coralph.config.json:
# {
#   "LoopOptions": {
#     "DockerImage": "coralph-dotnet-copilot:10.0",
#     "ToolAllow": [ "list_open_issues", "get_progress_summary", "search_progress" ],
#     "ToolDeny": [ "bash" ]
#   }
# }
```

## Documentation

- **[Architecture](docs/architecture.md)** - High-level architecture diagrams
  and component descriptions
- **[Using Coralph with Other Repositories](docs/using-with-other-repos.md)** -
  Adapt Coralph for Python, JavaScript, Go, and other tech stacks
- **[Changelog](CHANGELOG.md)** - Release history and notable changes

## Features

### Azure DevOps and Azure Boards Integration
Coralph supports fetching work items from Azure Boards as an alternative to GitHub issues:
- **Azure CLI integration**: Uses `az boards query` with WIQL to fetch work items
- **Work item transformation**: Converts Azure Boards work items (PBIs, Bugs, Tasks) to GitHub-compatible issue format
- **Flexible configuration**: Supports organization/project overrides or uses `az devops` defaults
- **HTML description parsing**: Automatically strips HTML from Azure Boards descriptions

Prerequisites:
- Azure CLI (`az`) installed and authenticated
- Azure DevOps CLI extension: `az extension add --name azure-devops`
- (Optional) Configure defaults: `az devops configure --defaults organization=https://dev.azure.com/<org> project=<project>`

Example workflow:
```bash
# Fetch work items from Azure Boards (using az devops defaults)
coralph --refresh-issues-azdo

# Override organization and project
coralph --refresh-issues-azdo --azdo-organization https://dev.azure.com/myorg --azdo-project MyProject

# Run the loop
coralph --max-iterations 10
```

### Direct Push Workflow
Coralph operates directly on the current branch (typically `main`). It commits and
pushes changes without creating separate review branches or running repository policy checks.

### Streaming Output Improvements

- **Visual styling**: Color-coded output for reasoning (cyan), assistant text
  (green), and tool execution (yellow)
- **Configuration**: Control display with `--show-reasoning` and
  `--colorized-output` flags
- **Mode tracking**: Automatic separation of reasoning vs. assistant vs. tool
  output

### Structured Event Stream (JSONL)

Enable structured streaming with `--stream-events true`. When enabled, Coralph
emits JSON objects (one per line) to stdout and routes human-friendly console
output to stderr. This makes it easy for UIs and integrations to parse output
while keeping terminal logs readable.

Example JSONL (truncated):
```json
{"type":"session","version":1,"id":"b7e4b9...","timestamp":"2026-02-01T12:34:56.789Z","cwd":"/path/to/repo","seq":1}
{"type":"turn_start","timestamp":"2026-02-01T12:34:57.012Z","sessionId":"b7e4b9...","seq":2,"turn":1,"maxIterations":10}
{"type":"message_start","timestamp":"2026-02-01T12:34:57.890Z","sessionId":"b7e4b9...","seq":3,"turn":1,"messageId":"assistant-1","message":{"id":"assistant-1","role":"assistant"}}
{"type":"message_update","timestamp":"2026-02-01T12:34:58.123Z","sessionId":"b7e4b9...","seq":4,"turn":1,"messageId":"assistant-1","delta":"Hello"}
{"type":"tool_execution_start","timestamp":"2026-02-01T12:34:58.456Z","sessionId":"b7e4b9...","seq":5,"turn":1,"toolCallId":"call-123","toolName":"list_open_issues","args":{"includeClosed":false}}
{"type":"tool_execution_end","timestamp":"2026-02-01T12:34:58.789Z","sessionId":"b7e4b9...","seq":6,"turn":1,"toolCallId":"call-123","toolName":"list_open_issues","success":true,"isError":false,"result":"..."}
{"type":"turn_end","timestamp":"2026-02-01T12:35:01.000Z","sessionId":"b7e4b9...","seq":7,"turn":1,"success":true,"output":"..."}
```

Event types include:
- session framing: `session`
- lifecycle: `agent_start`, `agent_end`, `turn_start`, `turn_end`
- messages: `message_start`, `message_update`, `message_end`
- tools: `tool_execution_start`, `tool_execution_update`, `tool_execution_end`
- system: `compaction_start`, `compaction_end`, `retry`, `session_usage`, `usage`

Consumers should treat unknown fields as optional to allow forward compatibility.

### Docker Sandbox Mode

Enable Docker sandboxing to run each loop iteration in an isolated container.
Coralph checks that Docker is installed and running before starting the loop.
The sandbox enables .NET roll-forward to prerelease runtimes so the devcontainer
image can run net10.0 apps without installing a GA runtime.

```bash
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true
```

You can override the image (default: `mcr.microsoft.com/devcontainers/dotnet:10.0`):

```bash
dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true --docker-image ghcr.io/devcontainers/dotnet:10.0
```

### Custom Tools

Built-in domain-specific tools available to the assistant:

- `list_open_issues`: Query issues from issues.json
- `get_progress_summary`: Retrieve recent progress entries
- `search_progress`: Search progress.txt for specific terms

### Tool Permission Policy

Control Copilot permission requests with `--tool-allow` and `--tool-deny`.
Rules match permission kinds and tool names. Deny rules win, and an allow list
denies everything else. Prefix matches are supported via `*` (for example,
`read_*`).

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

# Update changelog for a new version
just changelog v1.0.0

# Create a version tag (requires changelog entry)
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

Coralph follows a **local changelog generation** workflow to keep releases deterministic and reviewable:

1. **Update CHANGELOG.md locally**:

   ```bash
   # Generate changelog entry from git history
   just changelog v1.0.0
   
   # Review the changes
   git diff CHANGELOG.md
   
   # Commit and push to main
   git add CHANGELOG.md
   git commit -m "docs: update changelog for v1.0.0"
   git push
   ```
   
   The `just changelog` command:
   - Analyzes commits since the last tag
   - Categorizes commits by [Conventional Commits](https://www.conventionalcommits.org/) type:
     - `feat:` → Added section
     - `fix:` → Fixed section
     - `chore:`, `refactor:`, `perf:`, `style:` → Changed section
   - Creates anchored headings for deep linking (e.g., `v1-0-0`)
   - Updates comparison links at the bottom of CHANGELOG.md

2. **Create and push the tag**:

   ```bash
   just tag v1.0.0
   ```
   
   The `just tag` command will:
   - ✅ Validate that CHANGELOG.md has an entry for the version (fails if missing)
   - ✅ Create the git tag and push it to origin

3. **GitHub Actions automatically**:
   - Validates that CHANGELOG.md contains the version entry (fails if missing)
   - Builds self-contained binaries for all platforms
   - Extracts the changelog section for this version as release notes
   - Creates a GitHub Release with the version from the tag
   - Attaches platform-specific binaries to the release
   - Generates additional release notes from commits since the last release

### Why local changelog generation?

This workflow ensures:
- **No failed releases**: The pipeline validates (not generates) the changelog entry
- **Clear audit trail**: Changelog commits are separate from release tags
- **Human review**: Changes can be reviewed before they become part of the release

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
pushing your changes.
