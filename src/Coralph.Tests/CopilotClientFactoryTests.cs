using Coralph;
using GitHub.Copilot.SDK;

namespace Coralph.Tests;

public class CopilotClientFactoryTests
{
    [Fact]
    public void CreateClientOptions_WithExplicitValues_CopiesKnownFields()
    {
        var options = new LoopOptions
        {
            CliPath = "/usr/local/bin/copilot",
            CliUrl = "http://localhost:3000",
            CopilotToken = "ghp_test_token",
            TelemetryOtlpEndpoint = "http://localhost:4318",
            TelemetrySourceName = "coralph-test",
            TelemetryCaptureContent = true
        };

        var clientOptions = CopilotClientFactory.CreateClientOptions(options);

        Assert.False(string.IsNullOrWhiteSpace(clientOptions.Cwd));
        Assert.True(Path.IsPathRooted(clientOptions.Cwd));
        Assert.Equal("/usr/local/bin/copilot", clientOptions.CliPath);
        Assert.Equal("http://localhost:3000", clientOptions.CliUrl);
        Assert.Equal("ghp_test_token", clientOptions.GitHubToken);
        Assert.NotNull(clientOptions.Telemetry);
        Assert.Equal("http://localhost:4318", clientOptions.Telemetry!.OtlpEndpoint);
        Assert.Equal("coralph-test", clientOptions.Telemetry.SourceName);
        Assert.True(clientOptions.Telemetry.CaptureContent);
    }

    [Fact]
    public void CreateClientOptions_WithNullOptionalValues_LeavesThemUnset()
    {
        var clientOptions = CopilotClientFactory.CreateClientOptions(new LoopOptions());

        Assert.False(string.IsNullOrWhiteSpace(clientOptions.Cwd));
        Assert.True(Path.IsPathRooted(clientOptions.Cwd));
        Assert.Null(clientOptions.CliPath);
        Assert.Null(clientOptions.CliUrl);
        Assert.Null(clientOptions.GitHubToken);
        Assert.Null(clientOptions.Telemetry);
    }

    [Fact]
    public void CreateSessionConfig_WithToolsAndProvider_CopiesExpectedFields()
    {
        var tools = CustomTools.GetDefaultTools("issues.json", "progress.txt", "generated_tasks.json");
        SessionEventHandler onEvent = _ => { };
        var options = new LoopOptions
        {
            Model = "GPT-5.1-Codex",
            ProviderType = "openrouter",
            ProviderApiKey = "sk-or-test",
            CopilotToken = "ghp_session_token",
            ClientName = "coralph-test",
            ReasoningEffort = "high"
        };

        var config = CopilotClientFactory.CreateSessionConfig(options, tools, PermissionHandler.ApproveAll, onEvent);

        Assert.Equal("GPT-5.1-Codex", config.Model);
        Assert.True(config.Streaming);
        Assert.Same(tools, config.Tools);
        Assert.Same(PermissionHandler.ApproveAll, config.OnPermissionRequest);
        Assert.Same(onEvent, config.OnEvent);
        Assert.NotNull(config.Provider);
        Assert.Equal("openrouter", config.Provider!.Type);
        Assert.Equal("https://openrouter.ai/api/v1", config.Provider.BaseUrl);
        Assert.Equal("sk-or-test", config.Provider.ApiKey);
        Assert.Equal("ghp_session_token", config.GitHubToken);
        Assert.Equal("coralph-test", config.ClientName);
        Assert.Equal("high", config.ReasoningEffort);
        Assert.True(config.IncludeSubAgentStreamingEvents);
        Assert.NotNull(config.SystemMessage);
        Assert.Equal(SystemMessageMode.Customize, config.SystemMessage!.Mode);
        Assert.NotNull(config.SystemMessage.Sections);
        Assert.Contains(SystemPromptSections.Tone, config.SystemMessage.Sections.Keys);
        Assert.Contains(SystemPromptSections.Guidelines, config.SystemMessage.Sections.Keys);
        Assert.Contains(SystemPromptSections.ToolInstructions, config.SystemMessage.Sections.Keys);
        Assert.Contains(SystemPromptSections.Safety, config.SystemMessage.Sections.Keys);
    }

    [Fact]
    public void CreateSessionConfig_WithNullProviderValues_LeavesProviderUnset()
    {
        var tools = CustomTools.GetDefaultTools("issues.json", "progress.txt", "generated_tasks.json");
        var config = CopilotClientFactory.CreateSessionConfig(new LoopOptions(), tools, PermissionHandler.ApproveAll);

        Assert.True(config.Streaming);
        Assert.Same(tools, config.Tools);
        Assert.Null(config.Provider);
        Assert.Null(config.GitHubToken);
        Assert.True(config.IncludeSubAgentStreamingEvents);
        Assert.NotNull(config.SystemMessage);
    }
}
