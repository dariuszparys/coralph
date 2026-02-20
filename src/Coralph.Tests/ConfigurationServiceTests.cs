using Coralph;
using Coralph.Ui;

namespace Coralph.Tests;

public class ConfigurationServiceTests
{
    #region ApplyOverrides Tests

    [Fact]
    public void ApplyOverrides_WithMaxIterations_OverridesValue()
    {
        var options = new LoopOptions { MaxIterations = 10 };
        var overrides = new LoopOptionsOverrides { MaxIterations = 20 };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal(20, options.MaxIterations);
    }

    [Fact]
    public void ApplyOverrides_WithNullMaxIterations_KeepsOriginal()
    {
        var options = new LoopOptions { MaxIterations = 10 };
        var overrides = new LoopOptionsOverrides { MaxIterations = null };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal(10, options.MaxIterations);
    }

    [Fact]
    public void ApplyOverrides_WithModel_OverridesValue()
    {
        var options = new LoopOptions { Model = "old-model" };
        var overrides = new LoopOptionsOverrides { Model = "new-model" };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal("new-model", options.Model);
    }

    [Fact]
    public void ApplyOverrides_WithEmptyModel_KeepsOriginal()
    {
        var options = new LoopOptions { Model = "old-model" };
        var overrides = new LoopOptionsOverrides { Model = "" };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal("old-model", options.Model);
    }

    [Fact]
    public void ApplyOverrides_WithPromptFile_OverridesValue()
    {
        var options = new LoopOptions { PromptFile = "old.md" };
        var overrides = new LoopOptionsOverrides { PromptFile = "new.md" };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal("new.md", options.PromptFile);
    }

    [Fact]
    public void ApplyOverrides_WithRefreshIssues_OverridesValue()
    {
        var options = new LoopOptions { RefreshIssues = false };
        var overrides = new LoopOptionsOverrides { RefreshIssues = true };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.True(options.RefreshIssues);
    }

    [Fact]
    public void ApplyOverrides_WithShowReasoning_OverridesValue()
    {
        var options = new LoopOptions { ShowReasoning = true };
        var overrides = new LoopOptionsOverrides { ShowReasoning = false };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.False(options.ShowReasoning);
    }

    [Fact]
    public void ApplyOverrides_WithColorizedOutput_OverridesValue()
    {
        var options = new LoopOptions { ColorizedOutput = true };
        var overrides = new LoopOptionsOverrides { ColorizedOutput = false };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.False(options.ColorizedOutput);
    }

    [Fact]
    public void ApplyOverrides_WithUiMode_OverridesValue()
    {
        var options = new LoopOptions { UiMode = UiMode.Auto };
        var overrides = new LoopOptionsOverrides { UiMode = UiMode.Classic };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal(UiMode.Classic, options.UiMode);
    }

    [Fact]
    public void ApplyOverrides_WithDockerSandbox_OverridesValue()
    {
        var options = new LoopOptions { DockerSandbox = false };
        var overrides = new LoopOptionsOverrides { DockerSandbox = true };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.True(options.DockerSandbox);
    }

    [Fact]
    public void ApplyOverrides_WithDockerImage_OverridesValue()
    {
        var options = new LoopOptions { DockerImage = "default" };
        var overrides = new LoopOptionsOverrides { DockerImage = "ghcr.io/example/custom:latest" };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal("ghcr.io/example/custom:latest", options.DockerImage);
    }

    [Fact]
    public void ApplyOverrides_WithDemoMode_OverridesValue()
    {
        var options = new LoopOptions { DemoMode = false };
        var overrides = new LoopOptionsOverrides { DemoMode = true };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.True(options.DemoMode);
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
            GeneratedTasksFile = "test-generated-tasks.json",
            RefreshIssues = true,
            Repo = "test/repo",
            CliPath = "/path/to/cli",
            CliUrl = "http://localhost:8080",
            ShowReasoning = false,
            ColorizedOutput = false,
            UiMode = UiMode.Tui,
            DockerSandbox = true,
            DockerImage = "ghcr.io/example/custom:latest",
            DemoMode = true
        };

        ConfigurationService.ApplyOverrides(options, overrides);

        Assert.Equal(50, options.MaxIterations);
        Assert.Equal("test-model", options.Model);
        Assert.Equal("test.md", options.PromptFile);
        Assert.Equal("test-progress.txt", options.ProgressFile);
        Assert.Equal("test-issues.json", options.IssuesFile);
        Assert.Equal("test-generated-tasks.json", options.GeneratedTasksFile);
        Assert.True(options.RefreshIssues);
        Assert.Equal("test/repo", options.Repo);
        Assert.Equal("/path/to/cli", options.CliPath);
        Assert.Equal("http://localhost:8080", options.CliUrl);
        Assert.False(options.ShowReasoning);
        Assert.False(options.ColorizedOutput);
        Assert.Equal(UiMode.Tui, options.UiMode);
        Assert.True(options.DockerSandbox);
        Assert.Equal("ghcr.io/example/custom:latest", options.DockerImage);
        Assert.True(options.DemoMode);
    }

    #endregion

    [Fact]
    public void LoadOptions_WithUiModeInConfig_BindsUiMode()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"coralph-config-{Guid.NewGuid():N}.json");
        try
        {
            var json = """
                {
                  "LoopOptions": {
                    "UiMode": "classic"
                  }
                }
                """;
            File.WriteAllText(tempPath, json);

            var options = ConfigurationService.LoadOptions(new LoopOptionsOverrides(), tempPath);

            Assert.Equal(UiMode.Classic, options.UiMode);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void LoadOptions_WithUiModeOverride_OverrideWinsOverConfig()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"coralph-config-{Guid.NewGuid():N}.json");
        try
        {
            var json = """
                {
                  "LoopOptions": {
                    "UiMode": "classic"
                  }
                }
                """;
            File.WriteAllText(tempPath, json);

            var options = ConfigurationService.LoadOptions(
                new LoopOptionsOverrides { UiMode = UiMode.Tui },
                tempPath);

            Assert.Equal(UiMode.Tui, options.UiMode);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
