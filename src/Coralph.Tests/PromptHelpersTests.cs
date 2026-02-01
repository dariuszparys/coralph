using Coralph;

namespace Coralph.Tests;

public class PromptHelpersTests
{
    #region ContainsComplete Tests

    [Fact]
    public void ContainsComplete_WithPromiseTag_ReturnsTrue()
    {
        var output = "Some output\n<promise>COMPLETE</promise>\nMore output";

        Assert.True(PromptHelpers.ContainsComplete(output));
    }

    [Fact]
    public void ContainsComplete_WithPromiseTagCaseInsensitive_ReturnsTrue()
    {
        var output = "<Promise>complete</Promise>";

        Assert.True(PromptHelpers.ContainsComplete(output));
    }

    [Fact]
    public void ContainsComplete_WithPlainComplete_ReturnsTrue()
    {
        var output = "Some output\nCOMPLETE\nMore output";

        Assert.True(PromptHelpers.ContainsComplete(output));
    }

    [Fact]
    public void ContainsComplete_WithPlainCompleteLowercase_ReturnsTrue()
    {
        var output = "Some output\ncomplete\nMore output";

        Assert.True(PromptHelpers.ContainsComplete(output));
    }

    [Fact]
    public void ContainsComplete_WithoutComplete_ReturnsFalse()
    {
        var output = "Some regular output without the magic word";

        Assert.False(PromptHelpers.ContainsComplete(output));
    }

    [Fact]
    public void ContainsComplete_WithCompleteInWord_ReturnsFalse()
    {
        var output = "The task is completed successfully";

        Assert.False(PromptHelpers.ContainsComplete(output));
    }

    [Fact]
    public void ContainsComplete_WithEmptyString_ReturnsFalse()
    {
        Assert.False(PromptHelpers.ContainsComplete(""));
    }

    #endregion

    #region TryGetTerminalSignal Tests

    [Fact]
    public void TryGetTerminalSignal_WithAllTasksComplete_ReturnsSignal()
    {
        var output = "**ALL_TASKS_COMPLETE**";

        var result = PromptHelpers.TryGetTerminalSignal(output, out var signal);

        Assert.True(result);
        Assert.Equal("ALL_TASKS_COMPLETE", signal);
    }

    [Fact]
    public void TryGetTerminalSignal_WithNoOpenIssues_ReturnsSignal()
    {
        var output = "NO_OPEN_ISSUES";

        var result = PromptHelpers.TryGetTerminalSignal(output, out var signal);

        Assert.True(result);
        Assert.Equal("NO_OPEN_ISSUES", signal);
    }

    [Fact]
    public void TryGetTerminalSignal_WithPromiseComplete_ReturnsComplete()
    {
        var output = "<promise>COMPLETE</promise>";

        var result = PromptHelpers.TryGetTerminalSignal(output, out var signal);

        Assert.True(result);
        Assert.Equal("COMPLETE", signal);
    }

    [Fact]
    public void TryGetTerminalSignal_WithTokenInSentence_ReturnsFalse()
    {
        var output = "All tasks complete but not a sentinel";

        var result = PromptHelpers.TryGetTerminalSignal(output, out var signal);

        Assert.False(result);
        Assert.Equal(string.Empty, signal);
    }

    #endregion

    #region TryGetHasOpenIssues Tests

    [Fact]
    public void TryGetHasOpenIssues_WithEmptyString_ReturnsTrue()
    {
        var result = PromptHelpers.TryGetHasOpenIssues("", out var hasOpen, out var error);

        Assert.True(result);
        Assert.False(hasOpen);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetHasOpenIssues_WithEmptyArray_ReturnsTrue()
    {
        var result = PromptHelpers.TryGetHasOpenIssues("[]", out var hasOpen, out var error);

        Assert.True(result);
        Assert.False(hasOpen);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetHasOpenIssues_WithOpenIssue_ReturnsHasOpen()
    {
        var json = """[{"number": 1, "title": "Test", "state": "open"}]""";

        var result = PromptHelpers.TryGetHasOpenIssues(json, out var hasOpen, out var error);

        Assert.True(result);
        Assert.True(hasOpen);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetHasOpenIssues_WithClosedIssue_ReturnsNoOpen()
    {
        var json = """[{"number": 1, "title": "Test", "state": "closed"}]""";

        var result = PromptHelpers.TryGetHasOpenIssues(json, out var hasOpen, out var error);

        Assert.True(result);
        Assert.False(hasOpen);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetHasOpenIssues_WithMixedIssues_ReturnsHasOpen()
    {
        var json = """[{"number": 1, "state": "closed"}, {"number": 2, "state": "open"}]""";

        var result = PromptHelpers.TryGetHasOpenIssues(json, out var hasOpen, out var error);

        Assert.True(result);
        Assert.True(hasOpen);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetHasOpenIssues_WithNoState_DefaultsToOpen()
    {
        var json = """[{"number": 1, "title": "Test"}]""";

        var result = PromptHelpers.TryGetHasOpenIssues(json, out var hasOpen, out var error);

        Assert.True(result);
        Assert.True(hasOpen);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetHasOpenIssues_WithInvalidJson_ReturnsFalse()
    {
        var result = PromptHelpers.TryGetHasOpenIssues("not json", out var hasOpen, out var error);

        Assert.False(result);
        Assert.False(hasOpen);
        Assert.NotNull(error);
        Assert.Contains("Failed to parse", error);
    }

    [Fact]
    public void TryGetHasOpenIssues_WithNonArrayJson_ReturnsFalse()
    {
        var result = PromptHelpers.TryGetHasOpenIssues("{}", out var hasOpen, out var error);

        Assert.False(result);
        Assert.False(hasOpen);
        Assert.Contains("must be a JSON array", error);
    }

    #endregion

    #region BuildCombinedPrompt Tests

    [Fact]
    public void BuildCombinedPrompt_IncludesIssuesJson()
    {
        var prompt = PromptHelpers.BuildCombinedPrompt("template", "[{\"id\": 1}]", "progress");

        Assert.Contains("# ISSUES_JSON", prompt);
        Assert.Contains("{\"id\": 1}", prompt);
    }

    [Fact]
    public void BuildCombinedPrompt_IncludesProgress()
    {
        var prompt = PromptHelpers.BuildCombinedPrompt("template", "[]", "some progress");

        Assert.Contains("# PROGRESS_SO_FAR", prompt);
        Assert.Contains("some progress", prompt);
    }

    [Fact]
    public void BuildCombinedPrompt_WithEmptyProgress_ShowsEmpty()
    {
        var prompt = PromptHelpers.BuildCombinedPrompt("template", "[]", "");

        Assert.Contains("(empty)", prompt);
    }

    [Fact]
    public void BuildCombinedPrompt_IncludesInstructions()
    {
        var prompt = PromptHelpers.BuildCombinedPrompt("my template content", "[]", "");

        Assert.Contains("# INSTRUCTIONS", prompt);
        Assert.Contains("my template content", prompt);
    }

    [Fact]
    public void BuildCombinedPrompt_IncludesLoopContext()
    {
        var prompt = PromptHelpers.BuildCombinedPrompt("template", "[]", "");

        Assert.Contains("running inside a loop", prompt);
    }

    #endregion

    #region ApplyOverrides Tests

    [Fact]
    public void ApplyOverrides_WithMaxIterations_OverridesValue()
    {
        var options = new LoopOptions { MaxIterations = 10 };
        var overrides = new LoopOptionsOverrides { MaxIterations = 20 };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal(20, options.MaxIterations);
    }

    [Fact]
    public void ApplyOverrides_WithNullMaxIterations_KeepsOriginal()
    {
        var options = new LoopOptions { MaxIterations = 10 };
        var overrides = new LoopOptionsOverrides { MaxIterations = null };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal(10, options.MaxIterations);
    }

    [Fact]
    public void ApplyOverrides_WithModel_OverridesValue()
    {
        var options = new LoopOptions { Model = "old-model" };
        var overrides = new LoopOptionsOverrides { Model = "new-model" };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal("new-model", options.Model);
    }

    [Fact]
    public void ApplyOverrides_WithEmptyModel_KeepsOriginal()
    {
        var options = new LoopOptions { Model = "old-model" };
        var overrides = new LoopOptionsOverrides { Model = "" };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal("old-model", options.Model);
    }

    [Fact]
    public void ApplyOverrides_WithPromptFile_OverridesValue()
    {
        var options = new LoopOptions { PromptFile = "old.md" };
        var overrides = new LoopOptionsOverrides { PromptFile = "new.md" };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal("new.md", options.PromptFile);
    }

    [Fact]
    public void ApplyOverrides_WithRefreshIssues_OverridesValue()
    {
        var options = new LoopOptions { RefreshIssues = false };
        var overrides = new LoopOptionsOverrides { RefreshIssues = true };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.True(options.RefreshIssues);
    }

    [Fact]
    public void ApplyOverrides_WithShowReasoning_OverridesValue()
    {
        var options = new LoopOptions { ShowReasoning = true };
        var overrides = new LoopOptionsOverrides { ShowReasoning = false };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.False(options.ShowReasoning);
    }

    [Fact]
    public void ApplyOverrides_WithColorizedOutput_OverridesValue()
    {
        var options = new LoopOptions { ColorizedOutput = true };
        var overrides = new LoopOptionsOverrides { ColorizedOutput = false };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.False(options.ColorizedOutput);
    }

    [Fact]
    public void ApplyOverrides_WithPrModeBypassUsers_OverridesValue()
    {
        var options = new LoopOptions { PrModeBypassUsers = new List<string> { "existing" } };
        var overrides = new LoopOptionsOverrides { PrModeBypassUsers = new List<string> { "dariuszparys" } };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal(["dariuszparys"], options.PrModeBypassUsers);
    }

    [Fact]
    public void ApplyOverrides_WithDockerSandbox_OverridesValue()
    {
        var options = new LoopOptions { DockerSandbox = false };
        var overrides = new LoopOptionsOverrides { DockerSandbox = true };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.True(options.DockerSandbox);
    }

    [Fact]
    public void ApplyOverrides_WithDockerImage_OverridesValue()
    {
        var options = new LoopOptions { DockerImage = "default" };
        var overrides = new LoopOptionsOverrides { DockerImage = "ghcr.io/example/custom:latest" };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal("ghcr.io/example/custom:latest", options.DockerImage);
    }

    [Fact]
    public void ApplyOverrides_WithAllValues_OverridesAll()
    {
        var options = new LoopOptions();
        var overrides = new LoopOptionsOverrides
        {
            MaxIterations = 50,
            Model = "test-model",
            PromptFile = "test.md",
            ProgressFile = "test-progress.txt",
            IssuesFile = "test-issues.json",
            RefreshIssues = true,
            Repo = "test/repo",
            CliPath = "/path/to/cli",
            CliUrl = "http://localhost:8080",
            ShowReasoning = false,
            ColorizedOutput = false,
            PrModeBypassUsers = new List<string> { "dariuszparys" },
            DockerSandbox = true,
            DockerImage = "ghcr.io/example/custom:latest"
        };

        PromptHelpers.ApplyOverrides(options, overrides);

        Assert.Equal(50, options.MaxIterations);
        Assert.Equal("test-model", options.Model);
        Assert.Equal("test.md", options.PromptFile);
        Assert.Equal("test-progress.txt", options.ProgressFile);
        Assert.Equal("test-issues.json", options.IssuesFile);
        Assert.True(options.RefreshIssues);
        Assert.Equal("test/repo", options.Repo);
        Assert.Equal("/path/to/cli", options.CliPath);
        Assert.Equal("http://localhost:8080", options.CliUrl);
        Assert.False(options.ShowReasoning);
        Assert.False(options.ColorizedOutput);
        Assert.Equal(["dariuszparys"], options.PrModeBypassUsers);
        Assert.True(options.DockerSandbox);
        Assert.Equal("ghcr.io/example/custom:latest", options.DockerImage);
    }

    #endregion
}
