using Coralph;
using Coralph.Ui;

namespace Coralph.Tests;

public class ArgParserTests
{
    [Fact]
    public void Parse_WithNoArgs_ReturnsDefaultOverrides()
    {
        var (overrides, err, init, configFile, showHelp, showVersion) = ArgParser.Parse([]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.False(init);
        Assert.Null(configFile);
        Assert.False(showHelp);
        Assert.False(showVersion);
    }

    [Fact]
    public void Parse_WithHelpFlag_ReturnsShowHelp()
    {
        var (overrides, err, init, configFile, showHelp, showVersion) = ArgParser.Parse(["--help"]);

        Assert.Null(overrides);
        Assert.Null(err);
        Assert.True(showHelp);
        Assert.False(showVersion);
    }

    [Fact]
    public void Parse_WithShortHelpFlag_ReturnsShowHelp()
    {
        var (overrides, err, init, configFile, showHelp, showVersion) = ArgParser.Parse(["-h"]);

        Assert.Null(overrides);
        Assert.Null(err);
        Assert.True(showHelp);
        Assert.False(showVersion);
    }

    [Fact]
    public void Parse_WithVersionFlag_ReturnsShowVersion()
    {
        var (overrides, err, init, configFile, showHelp, showVersion) = ArgParser.Parse(["--version"]);

        Assert.Null(overrides);
        Assert.Null(err);
        Assert.False(showHelp);
        Assert.True(showVersion);
    }

    [Fact]
    public void Parse_WithShortVersionFlag_ReturnsShowVersion()
    {
        var (overrides, err, init, configFile, showHelp, showVersion) = ArgParser.Parse(["-v"]);

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
    public void Parse_WithProviderType_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--provider-type", "openai"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("openai", overrides.ProviderType);
    }

    [Fact]
    public void Parse_WithProviderApiKey_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--provider-api-key", "test-key"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("test-key", overrides.ProviderApiKey);
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
    public void Parse_WithGeneratedTasksFile_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--generated-tasks-file", "custom-generated-tasks.json"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("custom-generated-tasks.json", overrides.GeneratedTasksFile);
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
    public void Parse_WithInit_SetsFlag()
    {
        var (overrides, err, init, _, _, _) = ArgParser.Parse(["--init"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.True(init);
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
    public void Parse_WithWorkingDir_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--working-dir", "/tmp/repo"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("/tmp/repo", overrides.WorkingDir);
    }

    [Fact]
    public void Parse_WithEmptyWorkingDir_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--working-dir", ""]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--working-dir", err);
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
    public void Parse_WithUiMode_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--ui", "tui"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal(UiMode.Tui, overrides.UiMode);
    }

    [Fact]
    public void Parse_WithDemoMode_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--demo"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.True(overrides.DemoMode);
    }

    [Fact]
    public void Parse_WithInvalidUiMode_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--ui", "invalid-mode"]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--ui", err);
    }

    [Fact]
    public void Parse_WithStreamEvents_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--stream-events", "true"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.True(overrides.StreamEvents);
    }

    [Fact]
    public void Parse_WithDockerSandbox_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-sandbox", "true"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.True(overrides.DockerSandbox);
    }

    [Fact]
    public void Parse_WithDockerSandboxFalse_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-sandbox", "false"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.False(overrides.DockerSandbox);
    }

    [Fact]
    public void Parse_WithDockerImage_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-image", "ghcr.io/example/custom:latest"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("ghcr.io/example/custom:latest", overrides.DockerImage);
    }

    [Fact]
    public void Parse_WithEmptyDockerNetwork_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-network", ""]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--docker-network", err);
    }

    [Fact]
    public void Parse_WithDockerNetwork_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-network", "bridge"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("bridge", overrides.DockerNetworkMode);
    }

    [Fact]
    public void Parse_WithDockerMemory_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-memory", "4g"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("4g", overrides.DockerMemoryLimit);
    }

    [Fact]
    public void Parse_WithDockerCpus_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-cpus", "1.5"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("1.5", overrides.DockerCpuLimit);
    }

    [Fact]
    public void Parse_WithEmptyDockerImage_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--docker-image", ""]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--docker-image", err);
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
    public void Parse_WithClientName_SetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--client-name", "coralph-tests"]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("coralph-tests", overrides.ClientName);
    }

    [Fact]
    public void Parse_WithReasoningEffort_TrimsAndSetsOverride()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--reasoning-effort", "  high  "]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("high", overrides.ReasoningEffort);
    }

    [Fact]
    public void Parse_WithTelemetryOptions_SetsOverrides()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(
        [
            "--telemetry-otlp-endpoint", "http://localhost:4318",
            "--telemetry-source-name", "coralph-tests",
            "--telemetry-capture-content", "true"
        ]);

        Assert.NotNull(overrides);
        Assert.Null(err);
        Assert.Equal("http://localhost:4318", overrides.TelemetryOtlpEndpoint);
        Assert.Equal("coralph-tests", overrides.TelemetrySourceName);
        Assert.True(overrides.TelemetryCaptureContent);
    }

    [Fact]
    public void Parse_WithEmptyTelemetryEndpoint_ReturnsError()
    {
        var (overrides, err, _, _, _, _) = ArgParser.Parse(["--telemetry-otlp-endpoint", ""]);

        Assert.Null(overrides);
        Assert.NotNull(err);
        Assert.Contains("--telemetry-otlp-endpoint", err);
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
        Assert.Contains("--provider-type", output);
        Assert.Contains("--generated-tasks-file", output);
        Assert.Contains("--refresh-issues-azdo", output);
        Assert.Contains("--init", output);
        Assert.Contains("--working-dir", output);
        Assert.Contains("--azdo-organization", output);
        Assert.Contains("--azdo-project", output);
        Assert.Contains("--ui", output);
        Assert.Contains("--demo", output);
        Assert.Contains("--client-name", output);
        Assert.Contains("--reasoning-effort", output);
        Assert.Contains("--telemetry-otlp-endpoint", output);
        Assert.Contains("--telemetry-source-name", output);
        Assert.Contains("--telemetry-capture-content", output);
        Assert.Contains("--docker-network", output);
        Assert.Contains("--docker-memory", output);
        Assert.Contains("--docker-cpus", output);
    }

    [Fact]
    public void PrintUsage_DescribesDockerSandboxAsOptIn()
    {
        var sw = new StringWriter();

        ArgParser.PrintUsage(sw);

        var output = sw.ToString();
        Assert.Contains("--docker-sandbox", output);
        Assert.Contains("default: false", output);
        Assert.Contains("set true to opt into sandboxed execution", output);
    }

    [Fact]
    public void RegisteredOptions_IncludeHelpDriftOptions()
    {
        var optionNames = ArgParser.GetRegisteredOptionNames();

        Assert.Contains("--client-name", optionNames);
        Assert.Contains("--reasoning-effort", optionNames);
        Assert.Contains("--telemetry-otlp-endpoint", optionNames);
        Assert.Contains("--telemetry-source-name", optionNames);
        Assert.Contains("--telemetry-capture-content", optionNames);
        Assert.Contains("--docker-network", optionNames);
        Assert.Contains("--docker-memory", optionNames);
        Assert.Contains("--docker-cpus", optionNames);
        Assert.Equal(optionNames.Count, optionNames.Distinct(StringComparer.Ordinal).Count());
    }
}
