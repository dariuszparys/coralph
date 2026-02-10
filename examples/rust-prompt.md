# Rust Project Prompt Template

## Repo context

- Rust project using Cargo
- Source code in `src/` directory
- Tests in `tests/` directory or inline with `#[cfg(test)]`

## Build and test

- Build: `cargo build`
- Run tests: `cargo test`
- Lint: `cargo clippy`
- Format: `cargo fmt`
- Check without building: `cargo check`

## Project structure

```
├── src/
│   ├── main.rs (or lib.rs)
│   └── utils.rs
├── tests/
│   └── integration_test.rs
├── Cargo.toml
├── Cargo.lock
└── README.md
```

## Coding standards

- Follow Rust API Guidelines
- Use `Result` and `Option` types for error handling
- Write documentation comments (`///`) for public items
- Avoid `unwrap()` in production code
- Prefer iterators over manual loops

## Changes and logging

After completing work, append progress to `progress.txt` using the format in `prompt.md`.

# ISSUES

Issues JSON is provided at start of context. Parse it to get **OPEN** issues
with their bodies and comments.

`GENERATED_TASKS_JSON` is also provided. It contains persisted task splits for
the open issues. Treat it as the primary backlog for this loop.

**If there are no open issues, output "NO_OPEN_ISSUES" and stop immediately.**

# TASK BREAKDOWN

Use `GENERATED_TASKS_JSON` as the default task split. If an issue appears to be
under-split, propose finer-grained follow-up tasks in your summary.

Make each task the smallest possible unit of work. We don't want to outrun our
headlights. Aim for one small change per task.

# TASK SELECTION

Pick the next task from `GENERATED_TASKS_JSON` where status is `open` (or
`in_progress`). Prioritize tasks in this order:

1. Critical bugfixes
2. Tracer bullets for new features

Tracer bullets comes from the Pragmatic Programmer. When building systems, you
want to write code that gets you feedback as quickly as possible. Tracer bullets
are small slices of functionality that go through all layers of the system,
allowing you to test and validate your approach early. This helps in identifying
potential issues and ensures that the overall architecture is sound before
investing significant time in development.

TL;DR - build a tiny, end-to-end slice of the feature first, then expand it out.

3. Polish and quick wins
4. Refactors

**If no tasks remain from open issues in `GENERATED_TASKS_JSON`, output
"ALL_TASKS_COMPLETE" and stop immediately.**

# PRE-FLIGHT CHECK

Before starting work:

1. Verify the issue is still OPEN (check with
   `gh issue view <number> --json state`)
2. Check if the work was already done in a previous iteration (review recent
   commits and progress.txt)
3. If already done or issue is closed, skip to the next open issue or output
   "ALL_TASKS_COMPLETE"
4. Mark the selected task as `in_progress` in `generated_tasks.json`

# EXPLORATION

Explore the repo and fill your context window with relevant information that
will allow you to complete the task.

# EXECUTION

Complete the task.

If you find that the task is larger than you expected (for instance, requires a
refactor first), output "HANG_ON_A_SECOND".

Then, find a way to break it into smaller chunks and only do that chunk (i.e.
complete the smaller refactor).

When the task is completed in this iteration, mark it `done` in
`generated_tasks.json`. Leave any remaining tasks as `open`.

# FEEDBACK LOOPS

Before committing, run the feedback loops:

- `cargo test` to run the tests
- `cargo build` to run the build
- `cargo fmt --check` to verify formatting
- `cargo clippy -- -D warnings` to run lint checks

# PROGRESS

After completing, append to progress.txt:

```markdown
## [Date] - [GitHub Issue ID]

- What was implemented
- Files changed: [list]
- **Learnings:**
  - Any patterns discovered
  - Gotchas encountered
```

# COMMIT

Make a git commit using conventional commits. **Include progress.txt in your
commit** - ensure all changes including progress.txt are staged and committed:

- What was implemented
- Add key decisions made
- **Learnings:**
  - Any patterns discovered
  - Gotchas encountered

- Commit your changes to the current branch (typically main)
- Push the changes: `git push`

# CLOSE THE ISSUE

**Before closing, ALWAYS add a comment summarizing what was done:**
Use `gh issue comment <number> --body "Summary of changes"` to document:
- What was implemented or fixed
- Key files changed
- Any important decisions or gotchas

After commenting, close the issue using `gh issue close <number>`.

If the issue is not complete, leave a comment explaining what was done and what remains.

# FINAL RULES

- ONLY WORK ON A SINGLE GENERATED TASK PER ITERATION
- After completing one issue, DO NOT output COMPLETE - instead, the loop will
  continue and you will work on the next open issue in the next iteration
- Do NOT re-work already completed/closed issues
- Do NOT make unnecessary commits (like updating progress.txt for work already
  logged)
- If nothing needs to be done, output "ALL_TASKS_COMPLETE" and stop

# OUTPUT_RULES

- Work on ONE generated task per iteration. Make real changes to files.
- After making changes, summarize what you did and what remains.
- Only output <promise>COMPLETE</promise> when ALL of these are true:
  1. You made changes in THIS iteration (not just reviewed code)
  2. EVERY task in GENERATED_TASKS_JSON is done (not just the current one)
  3. There is genuinely no remaining work across ALL issues
  4. progress.txt has been updated AND committed (verify with `git status`)
- If you completed one issue but others remain open, do NOT output COMPLETE
- If unsure whether to output COMPLETE, do NOT output it - continue working.
