# Using Coralph with Other Repositories

## Overview

Coralph CLI is designed to work with any GitHub repository, not just .NET/C# projects. This guide explains how to adapt and configure Coralph for your specific tech stack.

## Prerequisites

### System Requirements
- **.NET SDK 10**: Only required if building Coralph from source (pre-built binaries don't need .NET installed)
- **GitHub CLI (`gh`)**: For issue synchronization (authenticated with `gh auth login`)
- **Target repository**: Clone of the repository you want to use with Coralph

### Important Distinction
While Coralph requires .NET to run, the **target repository** can use any tech stack (Python, JavaScript, Go, Rust, etc.). Coralph operates on your repository through shell commands and GitHub APIs.

## Quick Start

### 1. Install Coralph

**Option A: Download pre-built binary** (recommended)
```bash
# Download from https://github.com/dariuszparys/coralph/releases
# macOS/Linux: make executable
chmod +x coralph-linux-x64

# Move to your PATH (optional)
sudo mv coralph-linux-x64 /usr/local/bin/coralph
```

**Option B: Build from source**
```bash
git clone https://github.com/dariuszparys/coralph.git
cd coralph
dotnet publish src/Coralph -c Release -r linux-x64 --self-contained

# Binary will be in: src/Coralph/bin/Release/net10.0/linux-x64/publish/Coralph
```

### 2. Navigate to Your Target Repository

```bash
cd /path/to/your/target/repo
```

### 3. Initialize Coralph Files

Create the required files in your target repository:

```bash
# Initialize repository artifacts (issues.json, prompt.md, config, progress)
coralph --init

# Fetch issues from your GitHub repository
coralph --refresh-issues --repo owner/repo-name
```

### 4. Customize for Your Tech Stack

The key file to customize is `prompt.md`. This file instructs the AI assistant on how to work with your repository.

> **Note**: The `prompt.md` included in this repository is tailored for .NET projects (since Coralph itself is a .NET application). You'll need to adapt it for your specific tech stack by changing the build/test commands and project structure references.

**Example: Python Project**

Create `prompt.md` in your repository root:

```markdown
# INSTRUCTIONS

You are working on a Python project. Follow these conventions:

## Build and Test Commands
- Install dependencies: `pip install -r requirements.txt`
- Run tests: `pytest`
- Run linter: `flake8 .`
- Format code: `black .`

## Project Structure
- Source code: `src/`
- Tests: `tests/`
- Configuration: `pyproject.toml`

## Feedback Loops
Before committing, always run:
1. `pytest` - All tests must pass
2. `flake8 .` - No linting errors
3. `black . --check` - Code must be formatted

[Include the rest of the standard Coralph instructions from the original prompt.md]
```

**Example: JavaScript/Node.js Project**

```markdown
# INSTRUCTIONS

You are working on a Node.js project. Follow these conventions:

## Build and Test Commands
- Install dependencies: `npm install`
- Run tests: `npm test`
- Build: `npm run build`
- Lint: `npm run lint`

## Project Structure
- Source code: `src/`
- Tests: `__tests__/`
- Configuration: `package.json`, `tsconfig.json`

## Feedback Loops
Before committing, always run:
1. `npm test` - All tests must pass
2. `npm run lint` - No linting errors
3. `npm run build` - Build must succeed

[Include the rest of the standard Coralph instructions from the original prompt.md]
```

### 5. Run Coralph

```bash
# Start the loop (10 iterations max)
coralph --max-iterations 10

# Use custom config
coralph --config coralph.config.json --max-iterations 5
```

## Configuration Reference

### `coralph.config.json`

Customize behavior for your repository:

```json
{
  "model": "gpt-4o",
  "maxIterations": 10,
  "maxTokens": 100000,
  "issuesFile": "issues.json",
  "promptFile": "prompt.md",
  "progressFile": "progress.txt",
  "refreshIssues": false,
  "repo": "owner/repo-name",
  "showReasoning": true,
  "colorizedOutput": true
}
```

### Key Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `model` | `gpt-4o` | AI model to use |
| `maxIterations` | `10` | Maximum loop iterations |
| `promptFile` | `prompt.md` | Instructions file |
| `issuesFile` | `issues.json` | GitHub issues cache |
| `progressFile` | `progress.txt` | Progress tracking log |
| `repo` | (required for `--refresh-issues`) | GitHub repo `owner/name` |
| `showReasoning` | `true` | Display AI reasoning steps |
| `colorizedOutput` | `true` | Use colored terminal output |

## Adapting `prompt.md` for Your Stack

The `prompt.md` file is the most important customization point. Here's what to modify:

### 1. Feedback Loops Section

Replace .NET commands with your stack's commands:

**Before (.NET):**
```markdown
# FEEDBACK LOOPS

Before committing, run the feedback loops:
- `dotnet build` to run the build
- `dotnet test` to run the tests
```

**After (Python):**
```markdown
# FEEDBACK LOOPS

Before committing, run the feedback loops:
- `pytest` to run the tests
- `flake8 .` to check code quality
- `black . --check` to verify formatting
```

### 2. Project Structure Awareness

Add context about your repository layout:

```markdown
# PROJECT STRUCTURE

- `src/` - Source code
- `tests/` - Test files
- `docs/` - Documentation
- `scripts/` - Build and deployment scripts
- `.github/workflows/` - CI/CD workflows
```

### 3. Tech Stack Context

Provide information about your tools and frameworks:

```markdown
# TECH STACK

- Language: Python 3.11
- Framework: FastAPI
- Testing: pytest, pytest-cov
- Database: PostgreSQL
- ORM: SQLAlchemy
```

## Example Workflows

### Python Project Workflow

```bash
# 1. Setup
cd my-python-project
coralph --init
coralph --refresh-issues --repo myorg/my-python-project

# 2. Customize prompt.md for Python
# (edit prompt.md to use pytest, pip, etc.)

# 3. Run
coralph --max-iterations 5
```

### JavaScript/TypeScript Project Workflow

```bash
# 1. Setup
cd my-js-project
coralph --init
coralph --refresh-issues --repo myorg/my-js-project

# 2. Customize prompt.md for Node.js
# (edit prompt.md to use npm, jest, etc.)

# 3. Run
coralph --max-iterations 5
```

### Go Project Workflow

```bash
# 1. Setup
cd my-go-project
coralph --init
coralph --refresh-issues --repo myorg/my-go-project

# 2. Customize prompt.md for Go
# (edit prompt.md to use go build, go test, etc.)

# 3. Run
coralph --max-iterations 5
```

## Common Customization Patterns

### Multi-Stage Builds

For projects with complex build processes:

```markdown
# FEEDBACK LOOPS

Before committing:
1. `make clean` - Clean build artifacts
2. `make build` - Build the project
3. `make test` - Run all tests
4. `make lint` - Run linters
```

### Monorepo Support

For repositories with multiple projects:

```markdown
# MONOREPO STRUCTURE

This is a monorepo containing:
- `packages/api/` - Backend API (Node.js)
- `packages/web/` - Frontend (React)
- `packages/shared/` - Shared utilities

## Build Commands
- Build all: `npm run build`
- Test all: `npm run test`
- Build specific package: `npm run build --workspace=packages/api`
```

### Container-Based Development

For projects using Docker:

```markdown
# FEEDBACK LOOPS

Before committing:
1. `docker-compose build` - Build containers
2. `docker-compose up -d` - Start services
3. `docker-compose exec app pytest` - Run tests in container
4. `docker-compose down` - Stop services
```

## Troubleshooting

### Issue: AI doesn't follow build commands

**Solution**: Make build commands explicit and prominent in `prompt.md`. Add them to the FEEDBACK LOOPS section with clear expectations.

### Issue: AI modifies wrong files

**Solution**: Add a PROJECT STRUCTURE section to `prompt.md` specifying which directories contain source vs. generated code.

### Issue: Tests fail due to missing dependencies

**Solution**: Add a setup section to `prompt.md` instructing the AI to run dependency installation commands before testing.

### Issue: AI uses wrong language conventions

**Solution**: Add a CODING STANDARDS section to `prompt.md` with language-specific conventions (e.g., PEP 8 for Python, StandardJS for JavaScript).

## Best Practices

1. **Start with a template**: Copy the existing `prompt.md` from this repository and adapt it rather than starting from scratch.

2. **Be specific**: The more explicit your instructions in `prompt.md`, the better the AI will perform.

3. **Test incrementally**: Start with `--max-iterations 1` to verify your setup works before running longer loops.

4. **Review progress.txt**: Check the progress file after each iteration to see what the AI learned and accomplished.

5. **Keep config in version control**: Commit `prompt.md` and `coralph.config.json` to your repository so your team can use the same settings.

6. **Use .gitignore**: Add these entries to your `.gitignore`:
   ```
   issues.json
   progress.txt
   ```
   These files are local artifacts, not source code.

## Advanced: Language-Specific Templates

### Python Template

See [examples/python-prompt.md](../examples/python-prompt.md)

### JavaScript/TypeScript Template

See [examples/javascript-prompt.md](../examples/javascript-prompt.md)

### Go Template

See [examples/go-prompt.md](../examples/go-prompt.md)

### Rust Template

See [examples/rust-prompt.md](../examples/rust-prompt.md)

## Getting Help

- **GitHub Issues**: Report bugs or request features at [coralph/issues](https://github.com/dariuszparys/coralph/issues)
- **Discussions**: Share your `prompt.md` templates and configurations
- **Examples**: Check the `examples/` directory for reference implementations

## Summary

Coralph can work with any repository by:
1. Installing Coralph (requires .NET 10)
2. Navigating to your target repository
3. Creating `prompt.md` customized for your tech stack
4. Running `coralph --max-iterations N`

The key is adapting `prompt.md` to use your project's build, test, and deployment commands instead of the default .NET commands.
