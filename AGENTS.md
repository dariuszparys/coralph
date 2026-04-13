# AGENTS

## Overview
Coralph is a C#/.NET 10 CLI that runs a "Ralph loop" using the GitHub Copilot SDK.
Each iteration reads open issues, generates a task backlog, builds a combined prompt,
and sends it to Copilot for execution — committing changes, updating progress, and
closing issues autonomously.

## Core Commands
```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet format --verify-no-changes

# End-to-end
just ci
```

## Project Layout
- `src/Coralph` — main CLI application (~48 source files)
- `src/Coralph.Tests` — xUnit tests (~35 test files)
- `.devcontainer/` — VS Code dev container (`.NET 10 + GitHub CLI`)
- `.github/workflows` — CI, release, and maintenance pipelines
- `.github/scripts/` — Automation scripts (changelog, version bumping, validation)
- `.githooks/` — local Git hooks (opt-in)
- `docs/` — architecture diagrams, cross-repo usage guide, PowerShell reference
- `examples/` — language-specific `prompt.md` templates (Go, JS/TS, Python, Rust)
- `logs/` — structured JSON log files (daily rotation)
- `Dockerfile.copilot` — Docker image for sandboxed loop execution
- `justfile` — cross-platform task runner (build, test, ci, pack, publish, tag)
- `install.sh` — bash installation script
- `coralph-init`, `coralph-init.ps1` — project initialisation scripts (bash / PowerShell)

## High-Level Architecture

### Startup / Config Pipeline
`Program.cs` → `ArgParser.cs` (System.CommandLine) → `ConfigurationService.cs` → merged `LoopOptions`.
CLI flags override `coralph.config.json` values; built-in defaults fill any gaps.

### Iteration Pipeline
`LoopOrchestrator.cs` drives the main loop. Each iteration:
1. `LoopIterationState.cs` snapshots issues, progress, tasks, and git state.
2. `TaskBacklog.EnsureBacklogAsync` syncs `generated_tasks.json` from issue bodies.
3. `PromptHelpers.BuildCombinedPrompt` assembles the prompt with untrusted-input fencing.
4. `CopilotSessionRunner.RunTurnAsync` (or `CopilotRunner` for one-shot) streams the response.
5. `TerminalSignal.cs` decides whether to continue, stop, or flag a stall.

### Copilot Integration
| File | Role |
|------|------|
| `CopilotClientFactory.cs` | Builds `CopilotClientOptions` + `SessionConfig` |
| `CopilotSessionRunner.cs` | Multi-turn streaming session (primary path) |
| `CopilotRunner.cs` | One-shot execution path |
| `CopilotSessionEventRouter.cs` | Routes session events to output + event stream |
| `CopilotSystemMessageFactory.cs` | Builds system message with safety constraints |
| `CopilotModelDiscovery.cs` | Lists available models (`--list-models`) |
| `CopilotDiagnostics.cs` | Collects SDK + environment diagnostics |

### Tooling / Permissions
- `CustomTools.cs` exposes four read-only tools: `list_open_issues`, `list_generated_tasks`, `get_progress_summary`, `search_progress`.
- `PermissionPolicy.cs` gates tool requests with a 5-step evaluation: deny rules → allow rules → dangerous-tool defaults → explicit allow list → default allow. Dangerous patterns (`edit*`, `bash`, `execute*`, etc.) are denied by default unless the operator opts in with `--tool-allow`.

### Output / Telemetry
- `ConsoleOutput.cs` — static façade swapping between Classic (Spectre.Console text) and TUI (`Hex1bConsoleOutputBackend` via the Hex1b library) backends.
- `EventStreamWriter.cs` — optional JSONL event stream (schema v1).
- `Logging.cs` — Serilog compact JSON logs (`logs/coralph-{date}.log`, 7-day retention).
- `Banner.cs` — animated ASCII banner with version display.

### Docker Sandboxing
`DockerSandbox.cs` runs each iteration inside a container. Default image: `mcr.microsoft.com/devcontainers/dotnet:10.0`.
Docker sandbox is the default for unattended runs; opt out with `--docker-sandbox false`.
Copilot auth mounts are read-only; `GH_TOKEN` is only forwarded when `--copilot-token` is set explicitly.

### Provider Configuration
`ProviderConfigFactory.cs` supports `openai` and `openrouter` provider types.
Keys are read from `--provider-api-key`, `CORALPH_PROVIDER_API_KEY`, or `OPENROUTER_API_KEY` (for OpenRouter auto-detection).

## Execution Modes
| Command | Behaviour |
|---------|-----------|
| `dotnet run --project src/Coralph -- --max-iterations 10` | Main loop |
| `dotnet run --project src/Coralph -- --init` | Initialise a repository |
| `dotnet run --project src/Coralph -- --refresh-issues --repo owner/name` | Fetch GitHub issues |
| `dotnet run --project src/Coralph -- --refresh-issues-azdo --azdo-organization URL --azdo-project P` | Fetch Azure Boards items |
| `dotnet run --project src/Coralph -- --dry-run` | Preview mode (no writes/commits) |
| `dotnet run --project src/Coralph -- --demo` | Demo with mock data |
| `dotnet run --project src/Coralph -- --list-models` | List available models |
| `dotnet run --project src/Coralph -- --version` | Show version |

## Development Notes
- Tests live in `src/Coralph.Tests` and follow the `*Tests.cs` naming convention.
- No environment variables are required for local development.
- Optional env vars for provider integration: `CORALPH_PROVIDER_API_KEY`, `OPENROUTER_API_KEY`.
- The `CORALPH_DOCKER_SANDBOX` env var is set automatically inside sandbox containers.

## Key Conventions
- `prompt.md` is the source of truth for loop behaviour and terminal output rules.
- `generated_tasks.json` is managed by `TaskBacklog` and is the primary backlog during execution.
- Terminal signals (`COMPLETE`, `ALL_TASKS_COMPLETE`, `NO_OPEN_ISSUES`) are parsed from assistant output; `COMPLETE` is ignored when open tasks remain.
- When adding a CLI option, update: `ArgParser`, `LoopOptions`/`LoopOptionsOverrides`, `ConfigurationService.ApplyOverrides`, and related tests.
- Contribution workflow: direct push to `main` with Conventional Commits.

## Logging
Coralph uses Serilog for structured JSON logging.

- **Location**: `logs/coralph-{date}.log` (daily rotation, 7 days retention)
- **Format**: Compact JSON (one object per line)
- **Properties**: Application, Version, Model, Iteration (when applicable)

```csharp
Log.Information("Starting iteration {Iteration}", i);
Log.Error(ex, "Iteration {Iteration} failed", i);
using (LogContext.PushProperty("Iteration", i)) { /* scoped property */ }
```

## Git Hooks (Opt-in)
Enable local hooks:
```bash
git config core.hooksPath .githooks
```

The pre-commit hook runs:
- `dotnet format --verify-no-changes`
- a large-file size check for staged files
