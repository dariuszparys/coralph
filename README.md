# Coralph

A first cut of a “Ralph loop” runner implemented in C#/.NET 10 using the GitHub Copilot SDK.

## Prerequisites

- .NET SDK 10 preview
- GitHub CLI (`gh`) authenticated if you use `--refresh-issues`

## Run

```bash
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

# show the Coralph banner and version info
dotnet run --project src/Coralph -- --banner

# generate GitHub issues from a PRD markdown file
dotnet run --project src/Coralph -- --generate-issues --prd-file path/to/prd.md --repo owner/name

# customize streaming output
dotnet run --project src/Coralph -- --max-iterations 5 --show-reasoning false --verbose-tool-output

# filter available tools
dotnet run --project src/Coralph -- --max-iterations 5 --available-tools "bash,view,edit"

# use custom system message
dotnet run --project src/Coralph -- --max-iterations 5 --system-message-file custom-instructions.md
```

## Features

### Streaming Output Improvements
- **Visual styling**: Color-coded output for reasoning (cyan), assistant text (green), and tool execution (yellow)
- **Configuration**: Control display with `--show-reasoning`, `--verbose-tool-output`, and `--colorized-output` flags
- **Mode tracking**: Automatic separation of reasoning vs. assistant vs. tool output

### Custom Tools
Built-in domain-specific tools available to the assistant:
- `list_open_issues`: Query issues from issues.json
- `get_progress_summary`: Retrieve recent progress entries
- `search_progress`: Search progress.txt for specific terms

### Tool Filtering
- `--available-tools`: Comma-separated list of allowed tools (whitelist)
- `--excluded-tools`: Comma-separated list of blocked tools (blacklist)
- Configurable via CLI flags or `coralph.config.json`

### System Message Configuration
- `--system-message-file`: Load custom instructions from a file
- `--replace-system-message`: Replace (instead of append to) the default system message
- Supports both append mode (adds to defaults) and replace mode (full control)
```

## Build a distributable binary

```bash
# self-contained, single-file publish (adjust RID as needed: osx-arm64, osx-x64, linux-x64, win-x64)
dotnet publish src/Coralph -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

# run the published binary
./src/Coralph/bin/Release/net10.0/osx-arm64/publish/Coralph --max-iterations 5
```

Files used:
- `prompt.md` (instructions)
- `issues.json` (input; optional refresh via `gh`)
- `progress.txt` (append-only log)
- `coralph.config.json` (optional configuration overrides)
- `--prd-file` (input; used with `--generate-issues`)

The loop stops early when the assistant outputs a line containing `COMPLETE`, or when issues.json has no open issues (prints `NO_OPEN_ISSUES`).
