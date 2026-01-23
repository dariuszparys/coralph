# coralph

A first cut of a “Ralph loop” runner implemented in C#/.NET 10 using the GitHub Copilot SDK.

## Run

```bash
# optional: refresh issues.json using gh
(dotnet run --project src/Coralph -- --refresh-issues --repo owner/name) || true

# run loop (default reads ./issues.json)
dotnet run --project src/Coralph -- --max-iterations 10

# run loop using the bundled sample harness (no GitHub access needed)
dotnet run --project src/Coralph -- --issues-file issues.sample.json --max-iterations 10
```

Files used:
- `prompt.md` (instructions)
- `issues.json` (input; optional refresh via `gh`)
- `progress.txt` (append-only log)

The loop stops early when the assistant outputs a line containing `COMPLETE`.
