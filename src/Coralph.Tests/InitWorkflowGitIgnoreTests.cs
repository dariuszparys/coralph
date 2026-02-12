using Coralph;

namespace Coralph.Tests;

[Collection("InitWorkflowSerial")]
public sealed class InitWorkflowGitIgnoreTests : IDisposable
{
    private readonly string _originalWorkingDirectory;
    private readonly List<string> _tempDirectories = [];

    public InitWorkflowGitIgnoreTests()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
    }

    [Fact]
    public async Task RunAsync_CreatesGitIgnore_WhenMissing()
    {
        var repoRoot = CreateTempDirectory("coralph-init-gitignore-create");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "package.json"), """{ "name": "tmp", "version": "1.0.0" }""");

        var exitCode = await RunInitAsync(repoRoot);

        Assert.Equal(0, exitCode);
        var gitIgnorePath = Path.Combine(repoRoot, ".gitignore");
        Assert.True(File.Exists(gitIgnorePath));
        var gitIgnoreContent = await File.ReadAllTextAsync(gitIgnorePath);
        AssertManagedEntries(gitIgnoreContent, "Coralph*", "issues.json", "generated_tasks.json", "progress.txt");
    }

    [Fact]
    public async Task RunAsync_AppendsManagedBlock_WhenGitIgnoreExistsWithoutBlock()
    {
        var repoRoot = CreateTempDirectory("coralph-init-gitignore-append");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "package.json"), """{ "name": "tmp", "version": "1.0.0" }""");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, ".gitignore"), "bin/\nobj/\n");

        var exitCode = await RunInitAsync(repoRoot);

        Assert.Equal(0, exitCode);
        var gitIgnoreContent = await File.ReadAllTextAsync(Path.Combine(repoRoot, ".gitignore"));
        Assert.Contains("bin/", gitIgnoreContent, StringComparison.Ordinal);
        Assert.Contains("obj/", gitIgnoreContent, StringComparison.Ordinal);
        AssertManagedEntries(gitIgnoreContent, "Coralph*", "issues.json", "generated_tasks.json", "progress.txt");
    }

    [Fact]
    public async Task RunAsync_ReplacesManagedBlock_Idempotently()
    {
        var repoRoot = CreateTempDirectory("coralph-init-gitignore-replace");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "package.json"), """{ "name": "tmp", "version": "1.0.0" }""");
        var initial = """
            bin/
            # Coralph loop artifacts (managed)
            stale.json
            # End Coralph loop artifacts
            obj/
            """;
        await File.WriteAllTextAsync(Path.Combine(repoRoot, ".gitignore"), initial);

        var firstExitCode = await RunInitAsync(repoRoot);
        var afterFirstRun = await File.ReadAllTextAsync(Path.Combine(repoRoot, ".gitignore"));
        var secondExitCode = await RunInitAsync(repoRoot);
        var afterSecondRun = await File.ReadAllTextAsync(Path.Combine(repoRoot, ".gitignore"));

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        AssertManagedEntries(afterFirstRun, "Coralph*", "issues.json", "generated_tasks.json", "progress.txt");
        Assert.Equal(afterFirstRun, afterSecondRun);
        Assert.Contains("bin/", afterFirstRun, StringComparison.Ordinal);
        Assert.Contains("obj/", afterFirstRun, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ResolvesConfiguredPaths_ForIgnoreEntries()
    {
        var repoRoot = CreateTempDirectory("coralph-init-gitignore-config");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "package.json"), """{ "name": "tmp", "version": "1.0.0" }""");
        await File.WriteAllTextAsync(
            Path.Combine(repoRoot, "custom.config.json"),
            """
            {
              "LoopOptions": {
                "issuesFile": ".coralph/issues-cache.json",
                "generatedTasksFile": ".coralph/backlog/tasks.json",
                "progressFile": "notes/progress.log"
              }
            }
            """);

        var exitCode = await RunInitAsync(repoRoot, "custom.config.json");

        Assert.Equal(0, exitCode);
        var gitIgnoreContent = await File.ReadAllTextAsync(Path.Combine(repoRoot, ".gitignore"));
        AssertManagedEntries(
            gitIgnoreContent,
            "Coralph*",
            "issues.json",
            "generated_tasks.json",
            ".coralph/issues-cache.json",
            ".coralph/backlog/tasks.json",
            "notes/progress.log");
    }

    [Fact]
    public async Task RunAsync_DoesNotAddPromptOrConfig_ToIgnoreBlock()
    {
        var repoRoot = CreateTempDirectory("coralph-init-gitignore-scope");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "package.json"), """{ "name": "tmp", "version": "1.0.0" }""");

        var exitCode = await RunInitAsync(repoRoot);

        Assert.Equal(0, exitCode);
        var gitIgnoreContent = await File.ReadAllTextAsync(Path.Combine(repoRoot, ".gitignore"));
        AssertManagedEntries(gitIgnoreContent, "Coralph*", "issues.json", "generated_tasks.json", "progress.txt");
        Assert.DoesNotContain("prompt.md", gitIgnoreContent, StringComparison.Ordinal);
        Assert.DoesNotContain("coralph.config.json", gitIgnoreContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SkipsConfiguredEntriesOutsideRepo()
    {
        var repoRoot = CreateTempDirectory("coralph-init-gitignore-outside");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "package.json"), """{ "name": "tmp", "version": "1.0.0" }""");

        var outsidePath = Path.Combine(Path.GetTempPath(), $"coralph-outside-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            Path.Combine(repoRoot, "custom.config.json"),
            $$"""
            {
              "LoopOptions": {
                "issuesFile": "issues.json",
                "generatedTasksFile": "{{outsidePath.Replace("\\", "\\\\")}}",
                "progressFile": "progress.txt"
              }
            }
            """);

        var exitCode = await RunInitAsync(repoRoot, "custom.config.json");

        Assert.Equal(0, exitCode);
        var gitIgnoreContent = await File.ReadAllTextAsync(Path.Combine(repoRoot, ".gitignore"));
        AssertManagedEntries(gitIgnoreContent, "Coralph*", "issues.json", "generated_tasks.json", "progress.txt");
        Assert.DoesNotContain(outsidePath.Replace('\\', '/'), gitIgnoreContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithMalformedConfigFile_ReturnsFailureWithoutThrowing()
    {
        var repoRoot = CreateTempDirectory("coralph-init-gitignore-malformed-config");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "package.json"), """{ "name": "tmp", "version": "1.0.0" }""");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "coralph.config.json"), "{ invalid json");

        var exitCode = await RunInitAsync(repoRoot, "coralph.config.json");

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(Path.Combine(repoRoot, ".gitignore")));
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalWorkingDirectory);

        foreach (var path in _tempDirectories.Where(Directory.Exists))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private async Task<int> RunInitAsync(string repoRoot, string? configFile = null)
    {
        var previousWorkingDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(repoRoot);
            return await InitWorkflow.RunAsync(configFile);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousWorkingDirectory);
        }
    }

    private string CreateTempDirectory(string prefix)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        _tempDirectories.Add(tempDirectory);
        return tempDirectory;
    }

    private static void AssertManagedEntries(string gitIgnoreContent, params string[] expectedEntries)
    {
        const string blockStart = "# Coralph loop artifacts (managed)";
        const string blockEnd = "# End Coralph loop artifacts";

        var startIndex = gitIgnoreContent.IndexOf(blockStart, StringComparison.Ordinal);
        var endIndex = gitIgnoreContent.IndexOf(blockEnd, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, "Expected Coralph managed block start marker.");
        Assert.True(endIndex > startIndex, "Expected Coralph managed block end marker.");

        var block = gitIgnoreContent[startIndex..(endIndex + blockEnd.Length)];
        foreach (var entry in expectedEntries)
        {
            Assert.Contains(entry, block, StringComparison.Ordinal);
        }
    }
}
