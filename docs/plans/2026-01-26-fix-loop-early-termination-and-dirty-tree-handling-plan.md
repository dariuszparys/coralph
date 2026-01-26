---
title: Fix Loop Early Termination and Dirty Working Tree Handling
type: fix
date: 2026-01-26
---

# Fix Loop Early Termination and Dirty Working Tree Handling

## Overview

The Coralph loop runner exhibits three related issues:
1. **Early loop termination** - Loop exits at iteration 1/3 instead of running through all iterations
2. **Dirty working tree confusion** - The agent encounters uncommitted changes and stops
3. **Missing banner output** - The Figgle ASCII banner with version info not showing on startup

## Problem Analysis

### Issue 1: Loop Early Termination

**Root Cause:** The `ContainsComplete()` check at `Program.cs:97-100` terminates the loop when the AI model outputs `<promise>COMPLETE</promise>` AND the iteration count >= `minimumIterations`.

```csharp
if (ContainsComplete(output) && i >= minimumIterations)
{
    ConsoleOutput.WriteLine("\nCOMPLETE detected, stopping.\n");
    break;
}
```

**Current Behavior:**
- `minimumIterations = Math.Min(opt.MaxIterations, Math.Max(1, CountIssues(issues)))`
- If issues.json has 0-1 issues, `minimumIterations = 1`
- The AI model outputs `<promise>COMPLETE</promise>` on first iteration
- Loop exits immediately, ignoring `--max-iterations 3`

**Evidence from progress.txt:**
Most entries show `Iteration 1` immediately followed by `<promise>COMPLETE</promise>`, confirming the AI is outputting the sentinel immediately.

### Issue 2: Dirty Working Tree

**Root Cause:** The git status shows uncommitted changes:
```
M README.md
M coralph.config.json
M progress.txt
M src/Coralph.Tests/ToolOutputStylingTests.cs
M src/Coralph/ArgParser.cs
M src/Coralph/CopilotRunner.cs
M src/Coralph/LoopOptions.cs
M src/Coralph/Program.cs
?? src/Coralph/ConsoleOutput.cs
```

The Copilot agent encounters these pre-existing changes and becomes confused, asking "how to proceed" instead of working on the actual issues.

### Issue 3: Missing Banner

**Root Cause:** The `--banner` flag was added but defaults to `false`. The banner only shows when explicitly passed:
```csharp
if (opt.Banner)  // defaults to false
{
    var banner = FiggleFonts.Standard.Render("Coralph");
    ConsoleOutput.WriteLine(banner.TrimEnd());
    ConsoleOutput.WriteLine($"Coralph {GetVersionLabel()} | Model: {opt.Model}");
}
```

Your run showed the banner because the changes hadn't been committed yet, and the previous version always showed the banner.

## Technical Context

### Current Architecture

```
Program.cs (main entry)
    ├── ArgParser.cs (CLI parsing via System.CommandLine)
    ├── LoopOptions.cs (configuration model)
    ├── CopilotRunner.cs (GitHub Copilot SDK integration)
    ├── ConsoleOutput.cs (Spectre.Console wrapper) [UNTRACKED]
    └── GhIssues.cs (GitHub CLI integration)
```

### Spectre.Console Integration Status

**Yes, Spectre.Console is already integrated:**

1. `ConsoleOutput.cs:2` - `using Spectre.Console;`
2. `ConsoleOutput.cs:11-12` - Creates `IAnsiConsole` instances for stdout/stderr
3. `ConsoleOutput.cs:41` - `MarkupLine(string markup)` for styled output
4. `CopilotRunner.cs:107-108` - Uses `Markup.Escape()` and `MarkupLine()` for tool headers with `[black on orange1]` styling

## Proposed Solutions

### Fix 1: Improve Loop Termination Logic

**Option A: Add `--force-iterations` flag (Recommended)**
- New flag that ignores COMPLETE sentinel and always runs all iterations
- Preserves backward compatibility for existing behavior

**Option B: Require explicit COMPLETE count**
- Only exit early if COMPLETE appears N times consecutively
- More complex, may cause infinite loops

**Option C: Remove minimumIterations cap**
- Always run all iterations regardless of issue count
- Simpler but changes existing behavior

### Fix 2: Add Working Tree Check

**Option A: Warn and continue**
- Detect dirty working tree at startup
- Print warning but proceed with execution

**Option B: Abort on dirty tree (Recommended)**
- Add `--allow-dirty` flag to override
- Default: abort with clear error message explaining uncommitted changes

**Option C: Auto-stash changes**
- Automatically `git stash` before running
- Pop stash after completion
- Risky - could lose work on errors

### Fix 3: Banner Display

**Option A: Always show banner (Recommended)**
- Revert to always showing version info on startup
- Keep `--no-banner` or `--quiet` flag to suppress

**Option B: Keep current behavior**
- Banner only shows with `--banner` flag
- Document this clearly in README

## Acceptance Criteria

### Loop Termination Fix
- [ ] Loop runs all iterations when `--max-iterations N` is specified
- [ ] COMPLETE sentinel still works for graceful early exit when appropriate
- [ ] Add flag to force full iteration count: `--force-iterations` or `--ignore-complete`
- [ ] Unit test: verify loop completes N iterations with force flag

### Working Tree Check
- [ ] Detect uncommitted changes before starting loop
- [ ] Display clear error: "Error: Working tree has uncommitted changes. Commit or stash changes first, or use --allow-dirty to proceed."
- [ ] List modified files in error output
- [ ] `--allow-dirty` flag bypasses the check
- [ ] Unit test: verify dirty tree detection

### Banner Display
- [ ] Version and model always display on startup (not banner ASCII art)
- [ ] ASCII art banner controlled by `--banner` flag
- [ ] `--quiet` or `-q` flag suppresses all startup output

## Implementation Plan

### Phase 1: Core Loop Fix

**Files to modify:**
- `src/Coralph/Program.cs` - Add force-iterations logic
- `src/Coralph/LoopOptions.cs` - Add `ForceIterations` property
- `src/Coralph/ArgParser.cs` - Add `--force-iterations` flag

**Changes:**
```csharp
// Program.cs:97-101
if (ContainsComplete(output) && i >= minimumIterations && !opt.ForceIterations)
{
    ConsoleOutput.WriteLine("\nCOMPLETE detected, stopping.\n");
    break;
}
```

### Phase 2: Working Tree Check

**Files to modify:**
- `src/Coralph/Program.cs` - Add git status check before loop
- `src/Coralph/LoopOptions.cs` - Add `AllowDirty` property
- `src/Coralph/ArgParser.cs` - Add `--allow-dirty` flag

**New helper method:**
```csharp
static async Task<(bool IsDirty, string[] ModifiedFiles)> CheckWorkingTreeAsync(CancellationToken ct)
{
    var psi = new ProcessStartInfo("git", "status --porcelain")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    using var process = Process.Start(psi);
    var output = await process.StandardOutput.ReadToEndAsync(ct);
    await process.WaitForExitAsync(ct);

    var files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    return (files.Length > 0, files);
}
```

### Phase 3: Banner Improvements

**Files to modify:**
- `src/Coralph/Program.cs` - Always show version line, conditionally show ASCII art
- `src/Coralph/LoopOptions.cs` - Add `Quiet` property
- `src/Coralph/ArgParser.cs` - Add `--quiet` / `-q` flag

**Changes:**
```csharp
// Always show version unless quiet
if (!opt.Quiet)
{
    if (opt.Banner)
    {
        var banner = FiggleFonts.Standard.Render("Coralph");
        ConsoleOutput.WriteLine(banner.TrimEnd());
    }
    ConsoleOutput.WriteLine($"Coralph {GetVersionLabel()} | Model: {opt.Model}");
}
```

## Testing Strategy

### Manual Testing
```bash
# Test force iterations
dotnet run --project src/Coralph -- --max-iterations 3 --force-iterations

# Test dirty tree detection
git status  # ensure uncommitted changes
dotnet run --project src/Coralph -- --max-iterations 2
# Should fail with error about dirty tree

# Test allow-dirty bypass
dotnet run --project src/Coralph -- --max-iterations 2 --allow-dirty

# Test quiet mode
dotnet run --project src/Coralph -- --quiet --max-iterations 1
```

### Unit Tests
- [ ] `LoopOptionsTests.cs` - Test default values
- [ ] `ArgParserTests.cs` - Test new flag parsing
- [ ] `ProgramIntegrationTests.cs` - Test loop behavior with force flag

## Dependencies & Risks

**Dependencies:**
- Git must be available in PATH for working tree check
- No new NuGet packages required

**Risks:**
- Force iterations could cause infinite loops if COMPLETE is never output
- Working tree check adds startup latency (~50ms for git status)
- Changing default banner behavior may break scripts relying on output format

## References

### Internal References
- Loop logic: `src/Coralph/Program.cs:75-102`
- COMPLETE detection: `src/Coralph/Program.cs:392-400`
- Banner display: `src/Coralph/Program.cs:45-50`
- Spectre.Console wrapper: `src/Coralph/ConsoleOutput.cs:1-100`
- Tool styling: `src/Coralph/CopilotRunner.cs:94-109`

### Related Work
- Issue #7: "Ensure loop waits for all issues before honoring COMPLETE"
- Issue #23: "Add --banner flag"
