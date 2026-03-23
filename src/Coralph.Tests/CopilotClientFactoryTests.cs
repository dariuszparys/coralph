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
            CopilotToken = "ghp_test_token"
        };

        var clientOptions = CopilotClientFactory.CreateClientOptions(options);

        Assert.Equal(Directory.GetCurrentDirectory(), clientOptions.Cwd);
        Assert.Equal("/usr/local/bin/copilot", clientOptions.CliPath);
        Assert.Equal("http://localhost:3000", clientOptions.CliUrl);
        Assert.Equal("ghp_test_token", clientOptions.GitHubToken);
    }

    [Fact]
    public void CreateClientOptions_WithNullOptionalValues_LeavesThemUnset()
    {
        var clientOptions = CopilotClientFactory.CreateClientOptions(new LoopOptions());

        Assert.Equal(Directory.GetCurrentDirectory(), clientOptions.Cwd);
        Assert.Null(clientOptions.CliPath);
        Assert.Null(clientOptions.CliUrl);
        Assert.Null(clientOptions.GitHubToken);
    }

    [Fact]
    public void CreateSessionConfig_WithToolsAndProvider_CopiesExpectedFields()
    {
        var tools = CustomTools.GetDefaultTools("issues.json", "progress.txt", "generated_tasks.json");
        var options = new LoopOptions
        {
            Model = "GPT-5.1-Codex",
            ProviderType = "openrouter",
            ProviderApiKey = "sk-or-test",
            ClientName = "coralph-test",
            ReasoningEffort = "high"
        };

        var config = CopilotClientFactory.CreateSessionConfig(options, tools, PermissionHandler.ApproveAll);

        Assert.Equal("GPT-5.1-Codex", config.Model);
        Assert.True(config.Streaming);
        Assert.Same(tools, config.Tools);
        Assert.Same(PermissionHandler.ApproveAll, config.OnPermissionRequest);
        Assert.NotNull(config.Provider);
        Assert.Equal("openrouter", config.Provider!.Type);
        Assert.Equal("https://openrouter.ai/api/v1", config.Provider.BaseUrl);
        Assert.Equal("sk-or-test", config.Provider.ApiKey);
        Assert.Equal("coralph-test", config.ClientName);
        Assert.Equal("high", config.ReasoningEffort);
    }

    [Fact]
    public void CreateSessionConfig_WithNullProviderValues_LeavesProviderUnset()
    {
        var tools = CustomTools.GetDefaultTools("issues.json", "progress.txt", "generated_tasks.json");
        var config = CopilotClientFactory.CreateSessionConfig(new LoopOptions(), tools, PermissionHandler.ApproveAll);

        Assert.True(config.Streaming);
        Assert.Same(tools, config.Tools);
        Assert.Null(config.Provider);
    }
}
