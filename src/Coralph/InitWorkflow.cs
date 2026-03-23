using System.Text.Json;

namespace Coralph;

internal static class InitWorkflow
{
    private const string CoralphGitIgnoreBlockStart = "# Coralph loop artifacts (managed)";
    private const string CoralphGitIgnoreBlockEnd = "# End Coralph loop artifacts";

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

            var exitCode = 0;
            exitCode |= await EnsureIssuesFileAsync(repoRoot, coralphRoot);
            exitCode |= await EnsureConfigFileAsync(repoRoot, configFile);
            exitCode |= await EnsurePromptFileAsync(repoRoot, coralphRoot, projectType.Value);
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

    private static Task<int> EnsureIssuesFileAsync(string repoRoot, string coralphRoot)
    {
        var targetPath = Path.Combine(repoRoot, "issues.json");
        if (File.Exists(targetPath))
        {
            ConsoleOutput.WriteLine("issues.json already exists, skipping.");
            return Task.FromResult(0);
        }

        var sourcePath = Path.Combine(coralphRoot, "issues.sample.json");
        if (!File.Exists(sourcePath))
        {
            ConsoleOutput.WriteErrorLine($"Required init asset not found: {sourcePath}");
            return Task.FromResult(1);
        }

        try
        {
            File.Copy(sourcePath, targetPath);
            ConsoleOutput.WriteLine("Created issues.json");
            return Task.FromResult(0);
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write issues.json: {ex.Message}");
            return Task.FromResult(1);
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to write issues.json: {ex.Message}");
            return Task.FromResult(1);
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
        var json = JsonSerializer.Serialize(defaultPayload, JsonDefaults.Indented);
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

    private static async Task<int> EnsurePromptFileAsync(string repoRoot, string coralphRoot, ProjectType projectType)
    {
        var targetPath = Path.Combine(repoRoot, "prompt.md");
        if (File.Exists(targetPath))
        {
            ConsoleOutput.WriteLine("prompt.md already exists, skipping.");
            return 0;
        }

        try
        {
            var sourcePath = GetPromptTemplatePath(coralphRoot, projectType);
            var promptContent = await BuildPromptContentAsync(sourcePath, projectType).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(promptContent))
            {
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

    private static string GetPromptTemplatePath(string coralphRoot, ProjectType projectType)
    {
        return projectType switch
        {
            ProjectType.JavaScript => Path.Combine(coralphRoot, "examples", "javascript-prompt.md"),
            ProjectType.Python => Path.Combine(coralphRoot, "examples", "python-prompt.md"),
            ProjectType.Go => Path.Combine(coralphRoot, "examples", "go-prompt.md"),
            ProjectType.Rust => Path.Combine(coralphRoot, "examples", "rust-prompt.md"),
            ProjectType.DotNet => Path.Combine(coralphRoot, "prompt.md"),
            _ => Path.Combine(coralphRoot, "examples", "javascript-prompt.md")
        };
    }

    private static async Task<string?> BuildPromptContentAsync(string templatePath, ProjectType projectType)
    {
        if (!File.Exists(templatePath))
        {
            ConsoleOutput.WriteErrorLine($"Required init asset not found: {templatePath}");
            return null;
        }

        string templateContent;
        try
        {
            templateContent = await File.ReadAllTextAsync(templatePath, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to read prompt template: {ex.Message}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to read prompt template: {ex.Message}");
            return null;
        }

        if (projectType == ProjectType.DotNet)
        {
            return templateContent;
        }

        if (!ContainsCoreWorkflow(templateContent))
        {
            ConsoleOutput.WriteErrorLine($"Prompt template is missing core workflow sections: {templatePath}");
            return null;
        }

        var adaptedPrompt = AdaptCorePromptForProjectType(templateContent, projectType);
        if (string.IsNullOrWhiteSpace(adaptedPrompt))
        {
            ConsoleOutput.WriteErrorLine($"Prompt template could not be adapted for {projectType}: {templatePath}");
            return null;
        }

        return adaptedPrompt;
    }

    private static bool ContainsCoreWorkflow(string promptContent)
    {
        return promptContent.Contains("# ISSUES", StringComparison.Ordinal)
            && promptContent.Contains("# TASK BREAKDOWN", StringComparison.Ordinal);
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
