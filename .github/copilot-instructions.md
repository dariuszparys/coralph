# GitHub Copilot Instructions

## Repo context
- C#/.NET 10 solution: `Coralph.sln` with `src/Coralph` (app) and `src/Coralph.Tests` (xUnit tests).
- Core loop artifacts are `prompt.md`, `issues.json`, `generated_tasks.json`, `progress.txt`, and optional `coralph.config.json`.
- Terminal signals are parsed from assistant output: `COMPLETE`, `ALL_TASKS_COMPLETE`, `NO_OPEN_ISSUES`.

## Workflow expectations
- Follow `prompt.md` task rules: one small generated task per iteration, and update `generated_tasks.json` task status (`open`/`in_progress`/`done`).
- Prefer tracer-bullet vertical slices before broad feature work.
- Append structured progress entries to `progress.txt` using the format defined in `prompt.md`.

## Build and test
- Restore: `dotnet restore`
- Build (Debug): `dotnet build`
- Build (Release): `dotnet build -c Release` (or `just build`)
- Test (Debug): `dotnet test`
- Test (Release): `dotnet test -c Release` (or `just test`)
- Single test class: `dotnet test src/Coralph.Tests --filter "FullyQualifiedName~Coralph.Tests.TaskBacklogTests"`
- Single test method: `dotnet test src/Coralph.Tests --filter "FullyQualifiedName~Coralph.Tests.TaskBacklogTests.BuildBacklogJson_WithChecklistIssue_SplitsChecklistItemsIntoTasks"`
- Lint/format: `dotnet format --verify-no-changes`
- Full local CI recipe: `just ci`

## High-level architecture
- Startup/config pipeline: `Program.cs` parses CLI args (`ArgParser.cs`), loads config (`ConfigurationService.cs`), and resolves merged `LoopOptions` (CLI overrides config file).
- Iteration pipeline: each loop reloads issues/progress, calls `TaskBacklog.EnsureBacklogAsync` to sync `generated_tasks.json`, then builds a combined prompt via `PromptHelpers.BuildCombinedPrompt`.
- Copilot runtime: `CopilotSessionRunner.cs` keeps a streaming GitHub.Copilot.SDK session across iterations; `CopilotRunner.cs` handles one-shot execution paths.
- Tooling/permissions: `CustomTools.cs` exposes `list_open_issues`, `list_generated_tasks`, `get_progress_summary`, `search_progress`; `PermissionPolicy.cs` gates tool requests.
- Output/telemetry: `ConsoleOutput.cs` routes classic vs TUI output; optional `EventStreamWriter.cs` emits JSONL; `Logging.cs` writes compact JSON logs.

## Run loops
- Run loop: `dotnet run --project src/Coralph -- --max-iterations 10`
- Create default config: `dotnet run --project src/Coralph -- --init`
- Refresh GitHub issues (optional): `(dotnet run --project src/Coralph -- --refresh-issues --repo owner/name) || true`
- Refresh Azure Boards work items (optional): `dotnet run --project src/Coralph -- --refresh-issues-azdo --azdo-organization https://dev.azure.com/org --azdo-project Project`
- Run in Docker sandbox (optional): `dotnet run --project src/Coralph -- --max-iterations 5 --docker-sandbox true`

## Changes and logging
- `progress.txt` is append-only and should use the structured format from `prompt.md`.
- Structured logs are written to `logs/coralph-{date}.log` with properties like `Application`, `Version`, `Model`, and `Iteration`.

## Key conventions
- `prompt.md` is the source of truth for loop behavior and terminal output rules; keep workflow changes aligned with it.
- `generated_tasks.json` is managed by `TaskBacklog` and is treated as the primary backlog during loop execution.
- `COMPLETE` is ignored when open generated tasks remain (`Program.cs` checks backlog state before honoring it).
- When adding a CLI option, update all wiring points: `ArgParser.Parse`, `ArgParser.BuildRootCommand` (help output), `LoopOptions`/`LoopOptionsOverrides`, `ConfigurationService.ApplyOverrides`, and related tests.
- Tests follow `*Tests.cs` in `src/Coralph.Tests`; contribution workflow is direct push to `main` with Conventional Commits.
