using System.Text.Json;

namespace Coralph;

internal static class InitWorkflow
{
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

    internal static async Task<int> RunAsync(string? configFile)
    {
        ConsoleOutput.WriteLine("Initializing repository for Coralph...");
        var repoRoot = ResolveRepoRoot();
        if (repoRoot is null)
        {
            ConsoleOutput.WriteErrorLine("Current working directory is unavailable. Run --init from a valid repository path.");
            return 1;
        }

        var projectType = ResolveProjectType(repoRoot);
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

    private static ProjectType? ResolveProjectType(string repoRoot)
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

        return PromptProjectType();
    }

    private static ProjectType PromptProjectType()
    {
        ConsoleOutput.WriteLine("Could not automatically detect project type.");
        ConsoleOutput.WriteLine("Select your project type:");
        ConsoleOutput.WriteLine("  1) JavaScript/TypeScript");
        ConsoleOutput.WriteLine("  2) Python");
        ConsoleOutput.WriteLine("  3) Go");
        ConsoleOutput.WriteLine("  4) Rust");
        ConsoleOutput.WriteLine("  5) .NET");
        ConsoleOutput.WriteLine("  6) Other (use JavaScript/TypeScript template)");
        ConsoleOutput.Write("Enter number (1-6): ");

        var choice = Console.ReadLine()?.Trim();
        return choice switch
        {
            "1" => ProjectType.JavaScript,
            "2" => ProjectType.Python,
            "3" => ProjectType.Go,
            "4" => ProjectType.Rust,
            "5" => ProjectType.DotNet,
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

    private static async Task<int> EnsurePromptFileAsync(string repoRoot, string coralphRoot, ProjectType projectType)
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

        if (!File.Exists(sourcePath))
        {
            ConsoleOutput.WriteErrorLine($"Prompt template not found: {sourcePath}");
            return 1;
        }

        try
        {
            File.Copy(sourcePath, targetPath);
            ConsoleOutput.WriteLine($"Created prompt.md ({projectType} template)");
            await Task.CompletedTask;
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

    private enum ProjectType
    {
        JavaScript,
        Python,
        Go,
        Rust,
        DotNet
    }
}
