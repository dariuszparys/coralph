using Coralph;

namespace Coralph.Tests;

public class ArgParserTests
{
    [Fact]
    public void Parse_WithNoArgs_ReturnsDefaultOverrides()
    {
        var (overrides, err, initialConfig, configFile, showHelp, showVersion) = ArgParser.Parse([]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.False(initialConfig);
        Assert.Null(configFile);
        Assert.False(showHelp);
        Assert.False(showVersion);
    }

    [Fact]
    public void Parse_WithHelpFlag_ReturnsShowHelp()
    {
        var (overrides, err, initialConfig, configFile, showHelp, showVersion) = ArgParser.Parse(["--help"]);

        Assert.Null(overrides);
        Assert.Null(err);
        Assert.True(showHelp);
        Assert.False(showVersion);
    }

    [Fact]
    public void Parse_WithShortHelpFlag_ReturnsShowHelp()
    {
        var (overrides, err, initialConfig, configFile, showHelp, showVersion) = ArgParser.Parse(["-h"]);

        Assert.Null(overrides);
        Assert.Null(err);
        Assert.True(showHelp);
        Assert.False(showVersion);
    }

    [Fact]
    public void Parse_WithVersionFlag_ReturnsShowVersion()
    {
        var (overrides, err, initialConfig, configFile, showHelp, showVersion) = ArgParser.Parse(["--version"]);

        Assert.Null(overrides);
        Assert.Null(err);
        Assert.False(showHelp);
        Assert.True(showVersion);
    }

    [Fact]
    public void Parse_WithShortVersionFlag_ReturnsShowVersion()
    {
        var (overrides, err, initialConfig, configFile, showHelp, showVersion) = ArgParser.Parse(["-v"]);

        Assert.Null(overrides);
        Assert.Null(err);
        Assert.False(showHelp);
        Assert.True(showVersion);
    }

    [Fact]
    public void Parse_WithMaxIterations_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--max-iterations", "5"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal(5, overrides.MaxIterations);
    }

    [Fact]
    public void Parse_WithZeroMaxIterations_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--max-iterations", "0"]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--max-iterations", err);
    }

    [Fact]
    public void Parse_WithNegativeMaxIterations_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--max-iterations", "-1"]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--max-iterations", err);
    }

    [Fact]
    public void Parse_WithModel_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--model", "GPT-4"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("GPT-4", overrides.Model);
    }

    [Fact]
    public void Parse_WithEmptyModel_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--model", ""]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--model", err);
    }

    [Fact]
    public void Parse_WithPromptFile_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--prompt-file", "custom-prompt.md"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("custom-prompt.md", overrides.PromptFile);
    }

    [Fact]
    public void Parse_WithProgressFile_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--progress-file", "custom-progress.txt"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("custom-progress.txt", overrides.ProgressFile);
    }

    [Fact]
    public void Parse_WithIssuesFile_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--issues-file", "custom-issues.json"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("custom-issues.json", overrides.IssuesFile);
    }

    [Fact]
    public void Parse_WithRefreshIssues_SetsFlag()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--refresh-issues"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.True(overrides.RefreshIssues);
    }

    [Fact]
    public void Parse_WithRepo_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--repo", "owner/repo"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("owner/repo", overrides.Repo);
    }

    [Fact]
    public void Parse_WithInitialConfig_SetsFlag()
    {
        var (overrides, err, initialConfig, _, _, _) = ArgParser.Parse(["--initial-config"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.True(initialConfig);
    }

    [Fact]
    public void Parse_WithConfigFile_SetsConfigFile()
    {
        var (overrides, err, _, configFile, _, _) = ArgParser.Parse(["--config", "custom.config.json"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("custom.config.json", configFile);
    }

    [Fact]
    public void Parse_WithShowReasoning_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--show-reasoning", "false"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.False(overrides.ShowReasoning);
    }

    [Fact]
    public void Parse_WithColorizedOutput_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--colorized-output", "false"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.False(overrides.ColorizedOutput);
    }

    [Fact]
    public void Parse_WithMultipleOptions_SetsAllOverrides()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse([
            "--max-iterations", "20",
            "--model", "GPT-5",
            "--repo", "test/repo"
        ]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal(20, overrides.MaxIterations);
        Assert.Equal("GPT-5", overrides.Model);
        Assert.Equal("test/repo", overrides.Repo);
    }

    [Fact]
    public void Parse_WithCliPath_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--cli-path", "/usr/local/bin/copilot"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("/usr/local/bin/copilot", overrides.CliPath);
    }

    [Fact]
    public void Parse_WithCliUrl_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--cli-url", "http://localhost:8080"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("http://localhost:8080", overrides.CliUrl);
    }

    [Fact]
    public void PrintUsage_WritesToTextWriter()
    {
        var sw = new StringWriter();
        
        ArgParser.PrintUsage(sw);
        
        var output = sw.ToString();
        Assert.Contains("Coralph", output);
        Assert.Contains("--max-iterations", output);
        Assert.Contains("--model", output);
    }
}
