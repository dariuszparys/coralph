using System.Reflection;
using Coralph;

namespace Coralph.Tests;

[CollectionDefinition("InitWorkflowSerial", DisableParallelization = true)]
public sealed class InitWorkflowSerialCollection
{
}

[Collection("InitWorkflowSerial")]
public sealed class InitWorkflowPromptTemplateTests : IDisposable
{
    private static readonly MethodInfo EnsurePromptFileAsyncMethod =
        typeof(InitWorkflow).GetMethod("EnsurePromptFileAsync", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("InitWorkflow.EnsurePromptFileAsync was not found.");

    private static readonly Type ProjectTypeEnumType =
        typeof(InitWorkflow).GetNestedType("ProjectType", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("InitWorkflow.ProjectType enum was not found.");

    private readonly string _originalWorkingDirectory;
    private readonly List<string> _tempDirectories = [];

    public InitWorkflowPromptTemplateTests()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
    }

    public static IEnumerable<object[]> NonDotNetProjectCases()
    {
        yield return
        [
            "JavaScript",
            "package.json",
            "{ \"name\": \"tmp\", \"version\": \"1.0.0\" }",
            new[] { "`npm test`", "`npm run lint`", "`npm run build`" }
        ];
        yield return
        [
            "Python",
            "pyproject.toml",
            "[project]\nname = \"tmp\"\nversion = \"0.1.0\"",
            new[] { "`pytest`", "`flake8 .`", "`black . --check`" }
        ];
        yield return
        [
            "Go",
            "go.mod",
            "module example.com/tmp\n\ngo 1.22",
            new[] { "`go test ./...`", "`go build ./...`", "`gofmt -l .`", "`golangci-lint run`" }
        ];
        yield return
        [
            "Rust",
            "Cargo.toml",
            "[package]\nname = \"tmp\"\nversion = \"0.1.0\"\nedition = \"2021\"",
            new[] { "`cargo test`", "`cargo build`", "`cargo fmt --check`", "`cargo clippy -- -D warnings`" }
        ];
    }

    public static IEnumerable<object[]> NonDotNetFallbackCases()
    {
        yield return ["JavaScript", new[] { "`npm test`", "`npm run lint`", "`npm run build`" }];
        yield return ["Python", new[] { "`pytest`", "`flake8 .`", "`black . --check`" }];
        yield return ["Go", new[] { "`go test ./...`", "`go build ./...`", "`gofmt -l .`", "`golangci-lint run`" }];
        yield return ["Rust", new[] { "`cargo test`", "`cargo build`", "`cargo fmt --check`", "`cargo clippy -- -D warnings`" }];
    }

    public static IEnumerable<object[]> ExampleTemplateCases()
    {
        yield return ["javascript-prompt.md", new[] { "`npm test`", "`npm run lint`", "`npm run build`" }];
        yield return ["python-prompt.md", new[] { "`pytest`", "`flake8 .`", "`black . --check`" }];
        yield return ["go-prompt.md", new[] { "`go test ./...`", "`go build ./...`", "`gofmt -l .`", "`golangci-lint run`" }];
        yield return ["rust-prompt.md", new[] { "`cargo test`", "`cargo build`", "`cargo fmt --check`", "`cargo clippy -- -D warnings`" }];
    }

    [Theory]
    [MemberData(nameof(NonDotNetProjectCases))]
    public async Task RunAsync_NonDotNetProjects_UseLanguageSpecificCoreFeedbackLoops(
        string projectTypeName,
        string manifestFileName,
        string manifestContents,
        string[] expectedCommands)
    {
        var repoRoot = CreateTempDirectory("coralph-init-prompt");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, manifestFileName), manifestContents);

        var exitCode = await RunInitAsync(repoRoot);

        Assert.Equal(0, exitCode);
        var generatedPrompt = await File.ReadAllTextAsync(Path.Combine(repoRoot, "prompt.md"));
        AssertCoreSections(generatedPrompt);
        AssertAdaptedFeedbackLoops(generatedPrompt, expectedCommands, projectTypeName);
    }

    [Theory]
    [MemberData(nameof(NonDotNetFallbackCases))]
    public async Task EnsurePromptFileAsync_WithMissingAssets_UsesEmbeddedTemplateAndAdaptedCoreLoops(
        string projectTypeName,
        string[] expectedCommands)
    {
        var repoRoot = CreateTempDirectory("coralph-init-fallback-repo");
        var missingAssetsRoot = CreateTempDirectory("coralph-init-missing-assets");
        var fallbackPromptPath = Path.Combine(AppContext.BaseDirectory, "prompt.md");
        var fallbackPrompt = File.Exists(fallbackPromptPath)
            ? await File.ReadAllTextAsync(fallbackPromptPath)
            : string.Empty;

        var exitCode = await InvokeEnsurePromptFileAsync(repoRoot, missingAssetsRoot, projectTypeName, fallbackPrompt);

        Assert.Equal(0, exitCode);
        var generatedPrompt = await File.ReadAllTextAsync(Path.Combine(repoRoot, "prompt.md"));
        AssertCoreSections(generatedPrompt);
        AssertAdaptedFeedbackLoops(generatedPrompt, expectedCommands, projectTypeName);
    }

    [Fact]
    public async Task RunAsync_DotNetProject_KeepsDotNetCoreFeedbackLoops()
    {
        var repoRoot = CreateTempDirectory("coralph-init-dotnet");
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "Sample.sln"), string.Empty);

        var exitCode = await RunInitAsync(repoRoot);

        Assert.Equal(0, exitCode);
        var generatedPrompt = await File.ReadAllTextAsync(Path.Combine(repoRoot, "prompt.md"));
        AssertCoreSections(generatedPrompt);
        Assert.True(generatedPrompt.Contains("`dotnet build`", StringComparison.Ordinal));
        Assert.True(generatedPrompt.Contains("`dotnet test`", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ExampleTemplateCases))]
    public async Task ExampleTemplates_AreLanguageAdaptersWithSingleFeedbackLoopSource(
        string templateFileName,
        string[] expectedCommands)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "examples", templateFileName);
        Assert.True(File.Exists(templatePath), $"Missing template file: {templatePath}");
        var content = await File.ReadAllTextAsync(templatePath);

        AssertCoreSections(content);
        AssertAdaptedFeedbackLoops(content, expectedCommands, templateFileName);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalWorkingDirectory);

        foreach (var path in _tempDirectories.Where(Directory.Exists))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<int> InvokeEnsurePromptFileAsync(
        string repoRoot,
        string coralphRoot,
        string projectTypeName,
        string fallbackPrompt)
    {
        var projectType = Enum.Parse(ProjectTypeEnumType, projectTypeName);
        var task = (Task<int>)EnsurePromptFileAsyncMethod.Invoke(
            null,
            new object?[] { repoRoot, coralphRoot, projectType, fallbackPrompt })!;
        return await task;
    }

    private async Task<int> RunInitAsync(string repoRoot)
    {
        var previousWorkingDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(repoRoot);
            return await InitWorkflow.RunAsync(configFile: null);
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

    private static void AssertCoreSections(string content)
    {
        Assert.True(content.Contains("# ISSUES", StringComparison.Ordinal));
        Assert.True(content.Contains("# TASK BREAKDOWN", StringComparison.Ordinal));
        Assert.True(content.Contains("# FEEDBACK LOOPS", StringComparison.Ordinal));
        Assert.True(content.Contains("# PROGRESS", StringComparison.Ordinal));
    }

    private static void AssertAdaptedFeedbackLoops(string content, string[] expectedCommands, string label)
    {
        foreach (var command in expectedCommands)
        {
            Assert.True(content.Contains(command, StringComparison.Ordinal), $"Expected {label} prompt to contain {command}.");
        }

        Assert.False(content.Contains("`dotnet build`", StringComparison.Ordinal), $"Did not expect {label} prompt to include dotnet build.");
        Assert.False(content.Contains("`dotnet test`", StringComparison.Ordinal), $"Did not expect {label} prompt to include dotnet test.");
        Assert.False(content.Contains("## Feedback loops", StringComparison.Ordinal), $"Did not expect duplicate feedback loop section in {label} prompt.");
    }
}
