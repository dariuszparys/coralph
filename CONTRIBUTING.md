# Contributing to Coralph

Thank you for your interest in contributing to Coralph! This document outlines our contribution workflow and guidelines.

## PR-Based Workflow

**All contributions must be made through pull requests.** Direct pushes to the `main` branch are not permitted.

### Branch Protection Rules

Our `main` branch has the following protections enabled:

| Rule | Status | Description |
|------|--------|-------------|
| Required status checks | ✅ Enabled | CI must pass (`build-and-test`) |
| Required PR reviews | ✅ Enabled | At least 1 approval required |
| Enforce for admins | ✅ Enabled | No bypass for repository admins |
| Force pushes | ❌ Disabled | History cannot be rewritten |
| Branch deletion | ❌ Disabled | Main branch cannot be deleted |

### Contribution Process

1. **Create an issue** describing the bug or feature
2. **Fork the repository** or create a feature branch
3. **Make your changes** following our coding standards
4. **Run the tests** locally: `dotnet test`
5. **Open a pull request** targeting `main`
6. **Address review feedback** if requested
7. **Merge** once approved and CI passes

### Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add new feature
fix: resolve bug in X
docs: update README
refactor: restructure module Y
test: add tests for Z
```

## Development Setup

```bash
# Clone the repo
git clone https://github.com/dariuszparys/coralph.git
cd coralph

# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test
```

## Pre-Commit Hook (Optional)

Coralph includes a pre-commit hook that automatically runs code formatting and validation checks before each commit. This helps maintain consistent code style across the repository.

### What the Hook Does

The pre-commit hook (`.githooks/pre-commit`) performs two checks:

1. **Code formatting**: Runs `dotnet format --verify-no-changes` to ensure all staged files are properly formatted
2. **Large file detection**: Prevents commits of files larger than 1MB (to avoid accidental binary commits)

### How to Enable

The hook is **opt-in** to avoid disrupting your workflow. To enable it:

```bash
git config core.hooksPath .githooks
```

This configures Git to use the custom hooks in `.githooks/` instead of `.git/hooks/`.

### What to Expect

Once enabled, every `git commit` will:
- Run `dotnet format --verify-no-changes` on all staged files
- Check staged file sizes
- **Abort the commit** if formatting issues or large files are detected

If the hook blocks your commit:
- For formatting issues: Run `dotnet format` to auto-format your code, then try committing again
- For large files: Consider using Git LFS or reducing the file size

### Disabling the Hook

To disable the hook:

```bash
git config --unset core.hooksPath
```

## Audit Notes

This repository enforces strict PR-based development:

- **No push exceptions**: No users or apps have bypass permissions
- **No repository rulesets**: Branch protection is the only rule layer
- **Admin enforcement**: Even repository admins must go through PRs

If you encounter any issues with the contribution workflow, please open an issue.
