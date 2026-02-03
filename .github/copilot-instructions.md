# GitHub Copilot Instructions

## Repo context
- C#/.NET 10 solution: `Coralph.sln` with `src/Coralph` and `src/Coralph.Tests`.
- The loop runner uses `prompt.md`, `issues.json`, `progress.txt`, and optional `coralph.config.json`.
- The loop stops early when the assistant outputs a line containing `COMPLETE`.

## Workflow expectations
- Follow the task breakdown and selection guidance in `prompt.md` (one small task at a time).
- Prefer small, end-to-end tracer bullets for new features before larger expansions.
- If work grows unexpectedly, pause, split into a smaller chunk, and complete that chunk.

## Build and test
- Restore: `dotnet restore`
- Build: `dotnet build` (or `just build` for Release)
- Test: `dotnet test` (or `just test` for Release)
- Single test: `dotnet test src/Coralph.Tests --filter "FullyQualifiedName~Namespace.ClassName.TestName"`
- Lint/format: `dotnet format --verify-no-changes`

## High-level architecture
- CLI layer: `Program.cs` (entry), `ArgParser.cs` (System.CommandLine), `Banner.cs` (version/banner).
- Core loop: `LoopOptions.cs` (config), `PromptHelpers.cs` (prompt assembly), `CopilotRunner.cs` (session/streaming with GitHub.Copilot.SDK).
- I/O layer: `ConsoleOutput.cs` (Spectre.Console), `CustomTools.cs` (AI tools like list_open_issues), `GhIssues.cs` (gh CLI).
- File dependencies: `prompt.md`, `issues.json`, `progress.txt`, optional `coralph.config.json`.

## Run loops (common commands)
- Run loop: `dotnet run --project src/Coralph -- --max-iterations 10`
- Refresh issues (optional): `(dotnet run --project src/Coralph -- --refresh-issues --repo owner/name) || true`
- Create default config: `dotnet run --project src/Coralph -- --initial-config`

## Changes and logging
- After completing work, append progress to `progress.txt` using the format in `prompt.md`.

## Key conventions
- `prompt.md` is the source of truth for loop workflow (task breakdown/selection and output rules); keep changes aligned with it.
- `progress.txt` is an append-only learning log; entries follow the structured format in `prompt.md`.
- Direct push workflow with Conventional Commits is expected (`main` is updated directly).
- Tests live in `src/Coralph.Tests` and follow the `*Tests.cs` naming convention.
- Structured JSON logs are written to `logs/coralph-{date}.log` via Serilog (properties include Application, Version, Model, Iteration).
