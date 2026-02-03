# Contributing to Coralph

Thank you for your interest in contributing to Coralph! This document outlines our contribution workflow and guidelines.

## Direct Push Workflow

Coralph uses a direct push workflow. Changes are pushed straight to `main`
without mandatory reviews or policy gates.

### Contribution Process

1. **Create an issue** describing the bug or feature
2. **Make your changes** locally (on `main` or a short-lived branch)
3. **Run the tests** locally: `dotnet test`
4. **Commit** using Conventional Commits
5. **Push** to `main`

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

If you encounter any issues with the contribution workflow, please open an issue.
