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

## Audit Notes

This repository enforces strict PR-based development:

- **No push exceptions**: No users or apps have bypass permissions
- **No repository rulesets**: Branch protection is the only rule layer
- **Admin enforcement**: Even repository admins must go through PRs

If you encounter any issues with the contribution workflow, please open an issue.
