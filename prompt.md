# ISSUES

Issues JSON is provided at start of context. Parse it to get **OPEN** issues
with their bodies and comments.

**If there are no open issues, output "NO_OPEN_ISSUES" and stop immediately.**

# TASK BREAKDOWN

Break down the issues into tasks. An issue may contain a single task (a small
bugfix or visual tweak) or many, many tasks (a PRD or a large refactor).

Make each task the smallest possible unit of work. We don't want to outrun our
headlights. Aim for one small change per task.

# TASK SELECTION

Pick the next task. Prioritize tasks in this order:

1. Critical bugfixes
2. Tracer bullets for new features

Tracer bullets comes from the Pragmatic Programmer. When building systems, you
want to write code that gets you feedback as quickly as possible. Tracer bullets
are small slices of functionality that go through all layers of the system,
allowing you to test and validate your approach early. This helps in identifying
potential issues and ensures that the overall architecture is sound before
investing significant time in development.

TL;DR - build a tiny, end-to-end slice of the feature first, then expand it out.

1. Polish and quick wins
2. Refactors

**If no tasks remain from open issues, output "ALL_TASKS_COMPLETE" and stop
immediately.**

# PRE-FLIGHT CHECK

Before starting work:

1. Verify the issue is still OPEN (check with
   `gh issue view <number> --json state`)
2. Check if the work was already done in a previous iteration (review recent
   commits and progress.txt)
3. If already done or issue is closed, skip to the next open issue or output
   "ALL_TASKS_COMPLETE"

# EXPLORATION

Explore the repo and fill your context window with relevant information that
will allow you to complete the task.

# EXECUTION

Complete the task.

If you find that the task is larger than you expected (for instance, requires a
refactor first), output "HANG_ON_A_SECOND".

Then, find a way to break it into smaller chunks and only do that chunk (i.e.
complete the smaller refactor).

# FEEDBACK LOOPS

Before committing, run the feedback loops:

- `dotnet build` to run the build
- `dotnet test` to run the tests

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

Make a git commit using conventional commits:

- What was implemented
- Add key decisions made
- **Learnings:**
  - Any patterns discovered
  - Gotchas encountered

# CLOSE THE ISSUE

If the task is complete, close the original GitHub issue with
`gh issue close <number>`.

If the task is not complete, leave a comment on the GitHub issue with what was
done.

# FINAL RULES

- ONLY WORK ON A SINGLE TASK PER ITERATION
- Do NOT re-work already completed/closed issues
- Do NOT make unnecessary commits (like updating progress.txt for work already
  logged)
- If nothing needs to be done, output "ALL_TASKS_COMPLETE" and stop

# OUTPUT_RULES

- Work on ONE issue per iteration. Make real changes to files.
- After making changes, summarize what you did and what remains.
- Only output <promise>COMPLETE</promise> when ALL of these are true:
  1. You made changes in THIS iteration (not just reviewed code)
  2. Every issue in ISSUES_JSON has been addressed
  3. There is genuinely no remaining work
- If unsure whether to output COMPLETE, do NOT output it - continue working.
