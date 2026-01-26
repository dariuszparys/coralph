---
title: Fix Loop Early Termination (Minimal)
type: fix
date: 2026-01-26
---

# Fix Loop Early Termination (Minimal)

## Overview

The loop exits after iteration 1 instead of running through all iterations. This is caused by unnecessary `minimumIterations` logic and unclear prompting.

## The Fix

### 1. Remove `minimumIterations` Logic (2 lines)

**File:** `src/Coralph/Program.cs`

**Before (lines 73, 97-100):**
```csharp
var minimumIterations = Math.Min(opt.MaxIterations, Math.Max(1, CountIssues(issues)));
// ...
if (ContainsComplete(output) && i >= minimumIterations)
{
    ConsoleOutput.WriteLine("\nCOMPLETE detected, stopping.\n");
    break;
}
```

**After:**
```csharp
// Delete line 73 entirely

// Simplify line 97:
if (ContainsComplete(output))
{
    ConsoleOutput.WriteLine("\nCOMPLETE detected, stopping.\n");
    break;
}
```

**Rationale:** If a user passes `--max-iterations 3`, they want up to 3 iterations. The COMPLETE sentinel is for graceful early exit, not a minimum threshold. The `minimumIterations` calculation was premature optimization that causes confusion.

### 2. Clarify Prompt About Pre-existing Changes (1 line)

**File:** `src/Coralph/Program.cs` - `BuildCombinedPrompt()` method

**Add after line 147:**
```csharp
sb.AppendLine("Ignore any pre-existing uncommitted changes in the working tree - focus only on the issues listed above.");
```

**Rationale:** The AI agent gets confused by dirty working tree state. This is a prompt clarity issue, not a code issue. No flags or git checks needed.

### 3. Banner Default (Optional - 1 char)

**File:** `src/Coralph/LoopOptions.cs`

**Before (line 16):**
```csharp
public bool Banner { get; set; }
```

**After:**
```csharp
public bool Banner { get; set; } = true;
```

**Rationale:** If you want the banner to show by default, flip the default. If current behavior is fine, just document it. No new flags needed.

## Summary

| Change | File | Impact |
|--------|------|--------|
| Delete `minimumIterations` | Program.cs:73 | -1 line |
| Simplify COMPLETE check | Program.cs:97 | -3 chars |
| Add prompt clarification | Program.cs:148 | +1 line |
| (Optional) Banner default | LoopOptions.cs:16 | +7 chars |

**Total: ~5 lines changed. No new flags. No new classes. No new tests needed.**

## Verification

```bash
# Test loop runs all iterations
dotnet run --project src/Coralph -- --max-iterations 3 --refresh-issues

# Verify COMPLETE still works for early exit (when AI actually completes work)
# Check progress.txt shows iterations ran
```
