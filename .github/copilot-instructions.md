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
- Build: `dotnet build`
- Test: `dotnet test`

## Run loops (common commands)
- Run loop: `dotnet run --project src/Coralph -- --max-iterations 10`
- Refresh issues (optional): `(dotnet run --project src/Coralph -- --refresh-issues --repo owner/name) || true`
- Create default config: `dotnet run --project src/Coralph -- --initial-config`

## Changes and logging
- After completing work, append progress to `progress.txt` using the format in `prompt.md`.
