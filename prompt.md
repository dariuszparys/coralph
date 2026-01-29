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

1. **PR feedback** (if in PR mode and PR_FEEDBACK exists)
2. Critical bugfixes
3. Tracer bullets for new features

**PR Feedback**: In PR mode, issues with open PRs that have `@coralph` mentions
or unresolved review comments should be addressed promptly to unblock merges.

Tracer bullets comes from the Pragmatic Programmer. When building systems, you
want to write code that gets you feedback as quickly as possible. Tracer bullets
are small slices of functionality that go through all layers of the system,
allowing you to test and validate your approach early. This helps in identifying
potential issues and ensures that the overall architecture is sound before
investing significant time in development.

TL;DR - build a tiny, end-to-end slice of the feature first, then expand it out.

3. Polish and quick wins
4. Refactors

**If no tasks remain from open issues, output "ALL_TASKS_COMPLETE" and stop
immediately.**

# PRE-FLIGHT CHECK

Before starting work:

1. Verify the issue is still OPEN (check with
   `gh issue view <number> --json state`)
2. **In PR Mode**: Check if there's an open PR for this issue in the PR_FEEDBACK
   section above
   - If PR exists WITH feedback (`@coralph` mentions or unresolved comments):
     Continue - you'll address the feedback
   - If PR exists WITHOUT feedback: Skip this issue - it's waiting for human
     review
   - If NO PR exists: Continue - you'll create one
3. Check if the work was already done in a previous iteration (review recent
   commits and progress.txt)
4. If already done or issue is closed, skip to the next open issue or output
   "ALL_TASKS_COMPLETE"

# ADDRESSING PR FEEDBACK (PR Mode Only)

If the issue appears in the PR_FEEDBACK section:

1. Checkout the existing branch: `git checkout {prBranch}`
2. Review each feedback comment - they may reference specific files/lines
3. Make the requested changes
4. Commit and push: `git commit -m "fix: address PR feedback" && git push`
5. The PR will be updated automatically with your changes
6. Move to the next issue (do NOT close the issue - wait for PR merge)

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

Make a git commit using conventional commits. **Include progress.txt in your
commit** - ensure all changes including progress.txt are staged and committed:

- What was implemented
- Add key decisions made
- **Learnings:**
  - Any patterns discovered
  - Gotchas encountered

**In PR Mode:**
- Create a branch for the issue if it doesn't exist: `git checkout -b coralph/issue-{number}`
- If a branch already exists and has an open PR with feedback, checkout that branch
- Make your changes and commit them to the feature branch
- Push the branch: `git push -u origin coralph/issue-{number}`

**In Direct Push Mode (default):**
- Commit your changes to the current branch (typically main)
- Push the changes: `git push`

# CREATE OR UPDATE PULL REQUEST

**In PR Mode Only:**

If this is a NEW issue (no existing PR):
- Create a PR using: `gh pr create --fill --body "Fixes #{number}"`
- The "Fixes #{number}" in the body ensures the PR auto-closes the issue when merged

If addressing PR FEEDBACK (issue already has an open PR):
- Push your changes to the existing branch
- Optionally reply to resolved comments: `gh pr review {pr_number} --comment --body "Addressed: {summary}"`
- DO NOT create a new PR

# CLOSE THE ISSUE

**Direct Push Mode (default):**
Close the issue using `gh issue close <number>` after committing your changes.

**PR Mode:**
DO NOT close the issue directly. The PR will auto-close it when merged (via "Fixes #{number}" in the PR body).

If the issue is not complete, leave a comment on the GitHub issue with what was
done in either mode.

# FINAL RULES

- ONLY WORK ON A SINGLE TASK PER ITERATION
- After completing one issue, DO NOT output COMPLETE - instead, the loop will
  continue and you will work on the next open issue in the next iteration
- Do NOT re-work already completed/closed issues
- Do NOT make unnecessary commits (like updating progress.txt for work already
  logged)
- If nothing needs to be done, output "ALL_TASKS_COMPLETE" and stop

# OUTPUT_RULES

- Work on ONE issue per iteration. Make real changes to files.
- After making changes, summarize what you did and what remains.
- Only output <promise>COMPLETE</promise> when ALL of these are true:
  1. You made changes in THIS iteration (not just reviewed code)
  2. EVERY issue in ISSUES_JSON has been addressed (not just the current one)
  3. There is genuinely no remaining work across ALL issues
  4. progress.txt has been updated AND committed (verify with `git status`)
- If you completed one issue but others remain open, do NOT output COMPLETE
- If unsure whether to output COMPLETE, do NOT output it - continue working.
