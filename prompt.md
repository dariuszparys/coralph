# ISSUES

Issues JSON is provided at start of context. Parse it to get open issues with
their bodies and comments

# TASK BREAKDOWN

Break down the issues into tasks. An issue may contain a single task (a small
bugfix or visual tweak) or many, many tasks (a PRD or a large refactor).

Make each task the smalles possible unit of work. We don't want to outrun our
headlights. Aim for one small change per task.

# TASK SELECTION

Pick the next task. Prioritize tasks in this order:

1. Critical bufixes
2. Tracer bullets for new features

Tracer bullets comes from the Pragmatic Programmer. When building systems, you
want to write code that get you feedback as quickly as possible. Tracer bullets
are small slices of functionality that go through all layers of the system,
allowing you to test and validate your approach early. This helps in identifying
potential issues and ensures that the overall architecture is sound before
investing significant time in development.

TL;DR - build a tiny, end-to-end slice of the feature first, then expand it out.

1. Polish and quick wins
2. Refactors

if all tasks are complete, output <promise>COMPLETE</promise>.

# EXPLORATION

Explore the repo and fill your context window with relevant information that
will allow you to complete the task.

# EXECUTION

Complete the task.

If you find that the task is larger than you expected (for instance, requires a
refactor first), output "HANG ON A SECOND".

Then, find a way to break it into smaller chunk and only do that chunk (i.e.
complete the smaller refactor).

# FEEDBACK LOOPS

Before comitting, run the feedback loops:

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

Make a git commit with using conventional commits:

- What was implemented
- Add key decisions made
- **Learnings:**
  - Any patterns discovered
  - Gotchas encountered

# THE ISSUE

If the task is complete, close the original GitHub issue.

If the task is not complete, leave a comment on the GitHub issue with what was
done.

# FINAL RULES

ONLY WORK ON A SINGLE TASK.
