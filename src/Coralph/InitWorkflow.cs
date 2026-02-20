using System.Text.Json;

namespace Coralph;

internal static class InitWorkflow
{
    private const string CoralphGitIgnoreBlockStart = "# Coralph loop artifacts (managed)";
    private const string CoralphGitIgnoreBlockEnd = "# End Coralph loop artifacts";
    private const string EmbeddedIssuesSample = """
        [
          {
            "number": 101,
            "title": "Sample: Add hello command",
            "body": "Add a new CLI command `hello` that prints `hello` and exits.\n\n- [ ] Implement the command\n- [ ] Add usage docs",
            "url": "https://example.invalid/issues/101",
            "labels": [],
            "comments": []
          },
          {
            "number": 102,
            "title": "Sample: Fix typo in README",
            "body": "Fix a typo in the README.\n\n- [ ] Locate the typo\n- [ ] Correct it",
            "url": "https://example.invalid/issues/102",
            "labels": [],
            "comments": []
          }
        ]
        """;
    private const string EmbeddedCorePrompt = """
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
        
        - Commit your changes to the current branch (typically main)
        - Push the changes: `git push`
        
        # CLOSE THE ISSUE
        
        **Before closing, ALWAYS add a comment summarizing what was done:**
        Use `gh issue comment <number> --body-file /tmp/issue-comment.md` (create the file with a heredoc to preserve line breaks) to document:
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
        """;
    private const string EmbeddedJavaScriptPrompt = """
        # JavaScript/TypeScript Project Prompt Template
        
        ## Repo context
        
        - Node.js/TypeScript project
        - Source code in `src/` directory
        - Tests in `__tests__/` or `tests/` directory
        
        ## Build and test
        
        - Install dependencies: `npm install`
        - Run tests: `npm test`
        - Build: `npm run build`
        - Lint: `npm run lint`
        - Format: `npm run format` (if available)
        
        ## Project structure
        
        ```
        ├── src/
        │   ├── index.ts
        │   └── utils/
        │       └── helpers.ts
        ├── __tests__/
        │   └── index.test.ts
        ├── package.json
        ├── tsconfig.json
        ├── .eslintrc.js
        └── README.md
        ```

        ## Coding standards
        
        - Use ESLint and Prettier for consistent formatting
        - Prefer TypeScript over plain JavaScript
        - Use async/await over callbacks
        - Write Jest tests for new functionality
        
        ## Changes and logging
        
        After completing work, append progress to `progress.txt` using the format in `prompt.md`.
        """;
    private const string EmbeddedPythonPrompt = """
        # Python Project Prompt Template
        
        ## Repo context
        
        - Python project using standard tooling (pytest, flake8, black)
        - Source code in `src/` directory
        - Tests in `tests/` directory
        
        ## Build and test
        
        - Install dependencies: `pip install -r requirements.txt` or `pip install -e .`
        - Run tests: `pytest`
        - Run linter: `flake8 .`
        - Format code: `black .`
        - Type checking: `mypy src/`
        
        ## Project structure
        
        ```
        ├── src/
        │   └── my_package/
        │       ├── __init__.py
        │       └── main.py
        ├── tests/
        │   ├── __init__.py
        │   └── test_main.py
        ├── requirements.txt
        ├── pyproject.toml
        └── README.md
        ```

        ## Coding standards
        
        - Follow PEP 8 style guidelines
        - Use type hints for function signatures
        - Write docstrings for public functions and classes
        - Keep functions small and focused
        
        ## Changes and logging
        
        After completing work, append progress to `progress.txt` using the format in `prompt.md`.
        """;
    private const string EmbeddedGoPrompt = """
        # Go Project Prompt Template
        
        ## Repo context
        
        - Go project using standard Go toolchain
        - Source code in `cmd/` and `internal/` directories
        - Tests co-located with source files (`*_test.go`)
        
        ## Build and test
        
        - Build: `go build ./...`
        - Run tests: `go test ./...`
        - Run tests with coverage: `go test -cover ./...`
        - Lint: `golangci-lint run`
        - Format: `gofmt -w .`
        
        ## Project structure
        
        ```
        ├── cmd/
        │   └── myapp/
        │       └── main.go
        ├── internal/
        │   └── pkg/
        │       ├── handler.go
        │       └── handler_test.go
        ├── go.mod
        ├── go.sum
        └── README.md
        ```

        ## Coding standards
        
        - Follow Effective Go guidelines
        - Use meaningful package and function names
        - Keep functions small and focused
        - Write table-driven tests
        - Handle errors explicitly, don't ignore them
        
        ## Changes and logging
        
        After completing work, append progress to `progress.txt` using the format in `prompt.md`.
        """;
    private const string EmbeddedRustPrompt = """
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
        """;

    internal static async Task<int> RunAsync(string? configFile)
    {
        using var cts = new CancellationTokenSource();
        using var stopHandlerScope = ConsoleOutput.PushStopRequestHandler(() =>
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        });

        ConsoleCancelEventHandler cancelKeyPressHandler = (_, e) =>
        {
            e.Cancel = true;
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        };

        Console.CancelKeyPress += cancelKeyPressHandler;

        var ct = cts.Token;
        try
        {
            ConsoleOutput.WriteLine("Initializing repository for Coralph...");
            var repoRoot = ResolveRepoRoot();
            if (repoRoot is null)
            {
                ConsoleOutput.WriteErrorLine("Current working directory is unavailable. Run --init from a valid repository path.");
                return 1;
            }

            var projectType = await ResolveProjectTypeAsync(repoRoot, ct).ConfigureAwait(false);
            if (projectType is null)
            {
                ConsoleOutput.WriteErrorLine("Unable to determine project type. Create prompt.md manually or run from a repository root.");
                return 1;
            }

            ConsoleOutput.WriteLine($"Selected project type: {projectType}");

            var coralphRoot = AppContext.BaseDirectory;
            var fallbackPrompt = await TryReadFileAsync(Path.Combine(coralphRoot, "prompt.md")).ConfigureAwait(false);

            var exitCode = 0;
            exitCode |= await EnsureIssuesFileAsync(repoRoot, coralphRoot);
            exitCode |= await EnsureConfigFileAsync(repoRoot, configFile);
            exitCode |= await EnsurePromptFileAsync(repoRoot, coralphRoot, projectType.Value, fallbackPrompt);
            exitCode |= await EnsureProgressFileAsync(repoRoot);
            exitCode |= await EnsureGitIgnoreEntriesAsync(repoRoot, configFile);

            if (exitCode == 0)
            {
                ConsoleOutput.WriteLine("Initialization complete.");
                ConsoleOutput.WriteLine("Next steps:");
                ConsoleOutput.WriteLine("  1. Review and customize prompt.md for your project");
                ConsoleOutput.WriteLine("  2. Add your issues to issues.json (or use --refresh-issues)");
                ConsoleOutput.WriteLine("  3. Run: coralph --max-iterations 5");
            }
            else
            {
                ConsoleOutput.WriteErrorLine("Initialization failed. Review the errors above.");
            }

            return exitCode;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ConsoleOutput.WriteWarningLine("Initialization canceled.");
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= cancelKeyPressHandler;
        }
    }

    private static string? ResolveRepoRoot()
    {
        try
        {
            var cwd = Directory.GetCurrentDirectory();
            if (!string.IsNullOrWhiteSpace(cwd) && Directory.Exists(cwd))
            {
                return cwd;
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (FileNotFoundException)
        {
        }

        var pwd = Environment.GetEnvironmentVariable("PWD");
        if (!string.IsNullOrWhiteSpace(pwd) && Directory.Exists(pwd))
        {
            ConsoleOutput.WriteWarningLine("Current working directory is unavailable; using PWD for init.");
            return pwd;
        }

        return null;
    }

    private static async Task<ProjectType?> ResolveProjectTypeAsync(string repoRoot, CancellationToken ct)
    {
        var detected = DetectProjectType(repoRoot);
        if (detected is not null)
        {
            return detected;
        }

        if (Console.IsInputRedirected)
        {
            ConsoleOutput.WriteWarningLine("Could not detect project type; defaulting to JavaScript/TypeScript.");
            return ProjectType.JavaScript;
        }

        return await PromptProjectTypeAsync(ct).ConfigureAwait(false);
    }

    private static async Task<ProjectType> PromptProjectTypeAsync(CancellationToken ct)
    {
        var options = new[]
        {
            "JavaScript/TypeScript",
            "Python",
            "Go",
            "Rust",
            ".NET",
            "Other (use JavaScript/TypeScript template)"
        };

        var choice = await ConsoleOutput.PromptSelectionAsync(
            "Could not automatically detect project type.",
            options,
            defaultIndex: 0,
            ct).ConfigureAwait(false);

        return choice switch
        {
            0 => ProjectType.JavaScript,
            1 => ProjectType.Python,
            2 => ProjectType.Go,
            3 => ProjectType.Rust,
            4 => ProjectType.DotNet,
            _ => ProjectType.JavaScript
        };
    }

    private static ProjectType? DetectProjectType(string repoRoot)
    {
        if (File.Exists(Path.Combine(repoRoot, "package.json")))
            return ProjectType.JavaScript;
        if (File.Exists(Path.Combine(repoRoot, "pyproject.toml")) || File.Exists(Path.Combine(repoRoot, "requirements.txt")) || File.Exists(Path.Combine(repoRoot, "setup.py")))
            return ProjectType.Python;
        if (File.Exists(Path.Combine(repoRoot, "go.mod")))
            return ProjectType.Go;
        if (File.Exists(Path.Combine(repoRoot, "Cargo.toml")))
            return ProjectType.Rust;
        try
        {
            if (Directory.EnumerateFiles(repoRoot, "*.sln").Any() || Directory.EnumerateFiles(repoRoot, "*.csproj").Any())
                return ProjectType.DotNet;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        return null;
    }

    private static async Task<int> EnsureIssuesFileAsync(string repoRoot, string coralphRoot)
    {
        var targetPath = Path.Combine(repoRoot, "issues.json");
        if (File.Exists(targetPath))
        {
            ConsoleOutput.WriteLine("issues.json already exists, skipping.");
            return 0;
        }

        var sourcePath = Path.Combine(coralphRoot, "issues.sample.json");
        try
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath);
                ConsoleOutput.WriteLine("Created issues.json");
                await Task.CompletedTask;
                return 0;
            }

            await File.WriteAllTextAsync(targetPath, EmbeddedIssuesSample, CancellationToken.None);
            ConsoleOutput.WriteLine("Created issues.json (embedded sample)");
            return 0;
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write issues.json: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write issues.json: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> EnsureConfigFileAsync(string repoRoot, string? configFile)
    {
        var path = string.IsNullOrWhiteSpace(configFile)
            ? Path.Combine(repoRoot, LoopOptions.ConfigurationFileName)
            : (Path.IsPathRooted(configFile) ? configFile : Path.Combine(repoRoot, configFile));

        if (File.Exists(path))
        {
            ConsoleOutput.WriteLine($"Config file already exists, skipping: {path}");
            return 0;
        }

        var defaultPayload = new Dictionary<string, LoopOptions>
        {
            [LoopOptions.ConfigurationSectionName] = new LoopOptions()
        };
        var json = JsonSerializer.Serialize(defaultPayload, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            await File.WriteAllTextAsync(path, json, CancellationToken.None);
            ConsoleOutput.WriteLine($"Created config file: {path}");
            return 0;
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write config file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write config file: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> EnsurePromptFileAsync(string repoRoot, string coralphRoot, ProjectType projectType, string fallbackPrompt)
    {
        var targetPath = Path.Combine(repoRoot, "prompt.md");
        if (File.Exists(targetPath))
        {
            ConsoleOutput.WriteLine("prompt.md already exists, skipping.");
            return 0;
        }

        var sourcePath = projectType switch
        {
            ProjectType.JavaScript => Path.Combine(coralphRoot, "examples", "javascript-prompt.md"),
            ProjectType.Python => Path.Combine(coralphRoot, "examples", "python-prompt.md"),
            ProjectType.Go => Path.Combine(coralphRoot, "examples", "go-prompt.md"),
            ProjectType.Rust => Path.Combine(coralphRoot, "examples", "rust-prompt.md"),
            ProjectType.DotNet => Path.Combine(coralphRoot, "prompt.md"),
            _ => Path.Combine(coralphRoot, "examples", "javascript-prompt.md")
        };

        try
        {
            var promptContent = await BuildPromptContentAsync(projectType, coralphRoot, fallbackPrompt).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(promptContent))
            {
                ConsoleOutput.WriteErrorLine($"Prompt template not found: {sourcePath}");
                return 1;
            }

            await File.WriteAllTextAsync(targetPath, promptContent, CancellationToken.None).ConfigureAwait(false);
            ConsoleOutput.WriteLine($"Created prompt.md ({projectType} template)");
            return 0;
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write prompt.md: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write prompt.md: {ex.Message}");
            return 1;
        }
    }

    private static async Task<string> TryReadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return await File.ReadAllTextAsync(path, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static async Task<string> BuildPromptContentAsync(ProjectType projectType, string coralphRoot, string fallbackPrompt)
    {
        var templatePath = projectType switch
        {
            ProjectType.JavaScript => Path.Combine(coralphRoot, "examples", "javascript-prompt.md"),
            ProjectType.Python => Path.Combine(coralphRoot, "examples", "python-prompt.md"),
            ProjectType.Go => Path.Combine(coralphRoot, "examples", "go-prompt.md"),
            ProjectType.Rust => Path.Combine(coralphRoot, "examples", "rust-prompt.md"),
            ProjectType.DotNet => Path.Combine(coralphRoot, "prompt.md"),
            _ => Path.Combine(coralphRoot, "examples", "javascript-prompt.md")
        };

        var templateContent = await TryReadFileAsync(templatePath).ConfigureAwait(false);
        var corePrompt = string.IsNullOrWhiteSpace(fallbackPrompt) ? EmbeddedCorePrompt : fallbackPrompt;

        if (projectType == ProjectType.DotNet)
        {
            return string.IsNullOrWhiteSpace(templateContent) ? corePrompt : templateContent;
        }

        var adaptedCorePrompt = AdaptCorePromptForProjectType(corePrompt, projectType);

        if (!string.IsNullOrWhiteSpace(templateContent))
        {
            var cleanedTemplate = RemoveTemplateFeedbackLoopSection(templateContent);
            if (ContainsCoreWorkflow(cleanedTemplate))
            {
                return AdaptCorePromptForProjectType(cleanedTemplate, projectType);
            }

            if (string.IsNullOrWhiteSpace(adaptedCorePrompt))
            {
                return cleanedTemplate;
            }

            return $"{cleanedTemplate.TrimEnd()}\n\n{adaptedCorePrompt.TrimStart()}";
        }

        var embeddedTemplate = RemoveTemplateFeedbackLoopSection(GetEmbeddedPromptTemplate(projectType));
        if (!string.IsNullOrWhiteSpace(embeddedTemplate))
        {
            if (string.IsNullOrWhiteSpace(adaptedCorePrompt))
            {
                return embeddedTemplate;
            }

            return $"{embeddedTemplate.TrimEnd()}\n\n{adaptedCorePrompt.TrimStart()}";
        }

        return adaptedCorePrompt;
    }

    private static bool ContainsCoreWorkflow(string promptContent)
    {
        return promptContent.Contains("# ISSUES", StringComparison.Ordinal)
            && promptContent.Contains("# TASK BREAKDOWN", StringComparison.Ordinal);
    }

    private static string RemoveTemplateFeedbackLoopSection(string templateContent)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
        {
            return templateContent;
        }

        const string feedbackHeading = "## Feedback loops";
        var sectionStart = FindHeadingStart(templateContent, feedbackHeading);
        if (sectionStart < 0)
        {
            return templateContent;
        }

        var sectionEnd = FindNextSecondLevelHeading(templateContent, sectionStart + feedbackHeading.Length);
        if (sectionEnd < 0)
        {
            return templateContent[..sectionStart].TrimEnd();
        }

        return $"{templateContent[..sectionStart].TrimEnd()}\n\n{templateContent[sectionEnd..].TrimStart()}";
    }

    private static int FindHeadingStart(string content, string heading)
    {
        if (content.StartsWith(heading, StringComparison.Ordinal))
        {
            return 0;
        }

        var marker = $"\n{heading}";
        var markerIndex = content.IndexOf(marker, StringComparison.Ordinal);
        return markerIndex < 0 ? -1 : markerIndex + 1;
    }

    private static int FindNextSecondLevelHeading(string content, int searchStart)
    {
        var markerIndex = content.IndexOf("\n## ", searchStart, StringComparison.Ordinal);
        return markerIndex < 0 ? -1 : markerIndex + 1;
    }

    private static string AdaptCorePromptForProjectType(string promptContent, ProjectType projectType)
    {
        if (projectType == ProjectType.DotNet || string.IsNullOrWhiteSpace(promptContent))
        {
            return promptContent;
        }

        var feedbackLoopsSection = GetFeedbackLoopsSection(projectType);
        if (string.IsNullOrWhiteSpace(feedbackLoopsSection))
        {
            return promptContent;
        }

        const string feedbackHeading = "# FEEDBACK LOOPS";
        var feedbackStart = promptContent.IndexOf(feedbackHeading, StringComparison.Ordinal);
        if (feedbackStart < 0)
        {
            return promptContent;
        }

        var progressStart = promptContent.IndexOf("\n# PROGRESS", feedbackStart, StringComparison.Ordinal);
        if (progressStart < 0)
        {
            progressStart = promptContent.IndexOf("# PROGRESS", feedbackStart + feedbackHeading.Length, StringComparison.Ordinal);
            if (progressStart < 0)
            {
                return promptContent;
            }
        }

        var prefix = promptContent[..feedbackStart].TrimEnd();
        var suffix = promptContent[progressStart..].TrimStart();
        return $"{prefix}\n\n{feedbackLoopsSection.Trim()}\n\n{suffix}";
    }

    private static string GetFeedbackLoopsSection(ProjectType projectType)
    {
        return projectType switch
        {
            ProjectType.JavaScript => """
                # FEEDBACK LOOPS
                
                Before committing, run the feedback loops:
                
                - `npm test` to run the tests
                - `npm run lint` to check code quality
                - `npm run build` to run the build
                """,
            ProjectType.Python => """
                # FEEDBACK LOOPS
                
                Before committing, run the feedback loops:
                
                - `pytest` to run the tests
                - `flake8 .` to check code quality
                - `black . --check` to verify formatting
                """,
            ProjectType.Go => """
                # FEEDBACK LOOPS
                
                Before committing, run the feedback loops:
                
                - `go test ./...` to run the tests
                - `go build ./...` to run the build
                - `gofmt -l .` to verify formatting (output should be empty)
                - `golangci-lint run` to run lint checks (if available)
                """,
            ProjectType.Rust => """
                # FEEDBACK LOOPS
                
                Before committing, run the feedback loops:
                
                - `cargo test` to run the tests
                - `cargo build` to run the build
                - `cargo fmt --check` to verify formatting
                - `cargo clippy -- -D warnings` to run lint checks
                """,
            _ => string.Empty
        };
    }

    private static string GetEmbeddedPromptTemplate(ProjectType projectType)
    {
        return projectType switch
        {
            ProjectType.JavaScript => EmbeddedJavaScriptPrompt,
            ProjectType.Python => EmbeddedPythonPrompt,
            ProjectType.Go => EmbeddedGoPrompt,
            ProjectType.Rust => EmbeddedRustPrompt,
            _ => EmbeddedJavaScriptPrompt
        };
    }

    private static async Task<int> EnsureProgressFileAsync(string repoRoot)
    {
        var targetPath = Path.Combine(repoRoot, "progress.txt");
        if (File.Exists(targetPath))
        {
            ConsoleOutput.WriteLine("progress.txt already exists, skipping.");
            return 0;
        }

        try
        {
            await File.WriteAllTextAsync(targetPath, string.Empty, CancellationToken.None);
            ConsoleOutput.WriteLine("Created progress.txt");
            return 0;
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write progress.txt: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write progress.txt: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> EnsureGitIgnoreEntriesAsync(string repoRoot, string? configFile)
    {
        var gitIgnorePath = Path.Combine(repoRoot, ".gitignore");
        var gitIgnoreExisted = File.Exists(gitIgnorePath);

        try
        {
            var options = ConfigurationService.LoadOptions(new LoopOptionsOverrides(), configFile);
            var entries = ResolveGitIgnoreEntries(repoRoot, options);
            if (entries.Count == 0)
            {
                ConsoleOutput.WriteLine("No Coralph loop artifacts qualified for .gitignore update.");
                return 0;
            }

            var existingContent = File.Exists(gitIgnorePath)
                ? await File.ReadAllTextAsync(gitIgnorePath, CancellationToken.None).ConfigureAwait(false)
                : string.Empty;
            var mergedContent = MergeManagedGitIgnoreBlock(existingContent, entries);

            if (string.Equals(existingContent, mergedContent, StringComparison.Ordinal))
            {
                ConsoleOutput.WriteLine(".gitignore already contains Coralph loop artifact entries, skipping.");
                return 0;
            }

            await File.WriteAllTextAsync(gitIgnorePath, mergedContent, CancellationToken.None).ConfigureAwait(false);
            ConsoleOutput.WriteLine(gitIgnoreExisted
                ? "Updated .gitignore with Coralph loop artifact entries."
                : "Created .gitignore with Coralph loop artifact entries.");
            return 0;
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to update .gitignore: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to update .gitignore: {ex.Message}");
            return 1;
        }
        catch (InvalidDataException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to update .gitignore: {ex.Message}");
            return 1;
        }
        catch (FormatException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to update .gitignore: {ex.Message}");
            return 1;
        }
        catch (JsonException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to update .gitignore: {ex.Message}");
            return 1;
        }
    }

    private static List<string> ResolveGitIgnoreEntries(string repoRoot, LoopOptions options)
    {
        // Stable order for predictable block content.
        var candidates = new[]
        {
            "Coralph*",
            "issues.json",
            TaskBacklog.DefaultBacklogFile,
            options.IssuesFile,
            options.GeneratedTasksFile,
            options.ProgressFile
        };

        var entries = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            var normalized = NormalizeGitIgnorePath(repoRoot, candidate);
            if (normalized is null)
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                entries.Add(normalized);
            }
        }

        return entries;
    }

    private static string? NormalizeGitIgnorePath(string repoRoot, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repoRoot, path));
        var fullRepoRoot = Path.GetFullPath(repoRoot);
        var relative = Path.GetRelativePath(fullRepoRoot, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return null;
        }

        return relative.Replace('\\', '/');
    }

    private static string MergeManagedGitIgnoreBlock(string existingContent, IReadOnlyList<string> entries)
    {
        var newBlock = BuildManagedGitIgnoreBlock(entries);
        var startIndex = existingContent.IndexOf(CoralphGitIgnoreBlockStart, StringComparison.Ordinal);
        var endIndex = existingContent.IndexOf(CoralphGitIgnoreBlockEnd, StringComparison.Ordinal);

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var endLineIndex = existingContent.IndexOf('\n', endIndex);
            var replaceEnd = endLineIndex >= 0 ? endLineIndex + 1 : existingContent.Length;
            return $"{existingContent[..startIndex]}{newBlock}{existingContent[replaceEnd..]}".TrimEnd() + "\n";
        }

        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return newBlock;
        }

        return $"{existingContent.TrimEnd()}\n\n{newBlock}";
    }

    private static string BuildManagedGitIgnoreBlock(IReadOnlyList<string> entries)
    {
        var lines = new List<string>
        {
            CoralphGitIgnoreBlockStart
        };

        lines.AddRange(entries);
        lines.Add(CoralphGitIgnoreBlockEnd);
        return string.Join('\n', lines) + "\n";
    }

    private enum ProjectType
    {
        JavaScript,
        Python,
        Go,
        Rust,
        DotNet
    }
}
