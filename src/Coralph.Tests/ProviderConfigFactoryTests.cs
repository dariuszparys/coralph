using Coralph;

namespace Coralph.Tests;

public class ProviderConfigFactoryTests
{
    [Fact]
    public void Create_WithNoProviderSettings_ReturnsNull()
    {
        var options = new LoopOptions();

        var config = ProviderConfigFactory.Create(options);

        Assert.Null(config);
    }

    [Fact]
    public void Create_WithOnlyApiKey_DefaultsToOpenAi()
    {
        var options = new LoopOptions
        {
            ProviderApiKey = "test-key"
        };

        var config = ProviderConfigFactory.Create(options);

        Assert.NotNull(config);
        Assert.Equal("openai", config.Type);
        Assert.Equal("https://api.openai.com/v1/", config.BaseUrl);
        Assert.Equal("responses", config.WireApi);
        Assert.Equal("test-key", config.ApiKey);
    }

    [Fact]
    public void Create_WithCustomBaseUrlAndWireApi_PreservesValues()
    {
        var options = new LoopOptions
        {
            ProviderType = "openai",
            ProviderBaseUrl = "https://example.com/api/",
            ProviderWireApi = "chat"
        };

        var config = ProviderConfigFactory.Create(options);

        Assert.NotNull(config);
        Assert.Equal("openai", config.Type);
        Assert.Equal("https://example.com/api/", config.BaseUrl);
        Assert.Equal("chat", config.WireApi);
    }

    [Fact]
    public void Create_WithOpenAiTypeIsCaseInsensitive_DefaultsAreApplied()
    {
        var options = new LoopOptions
        {
            ProviderType = "OpenAI"
        };

        var config = ProviderConfigFactory.Create(options);

        Assert.NotNull(config);
        Assert.Equal("OpenAI", config.Type);
        Assert.Equal("https://api.openai.com/v1/", config.BaseUrl);
        Assert.Equal("responses", config.WireApi);
    }

    [Fact]
    public void Create_WithOpenRouterType_DefaultsBaseUrl()
    {
        var options = new LoopOptions
        {
            ProviderType = "openrouter",
            ProviderApiKey = "sk-or-test"
        };

        var config = ProviderConfigFactory.Create(options);

        Assert.NotNull(config);
        Assert.Equal("openrouter", config.Type);
        Assert.Equal("https://openrouter.ai/api/v1", config.BaseUrl);
        Assert.Equal("sk-or-test", config.ApiKey);
    }

    [Fact]
    public void Create_WithOpenRouterType_CustomBaseUrlIsPreserved()
    {
        var options = new LoopOptions
        {
            ProviderType = "openrouter",
            ProviderBaseUrl = "https://custom.openrouter.ai/v1"
        };

        var config = ProviderConfigFactory.Create(options);

        Assert.NotNull(config);
        Assert.Equal("openrouter", config.Type);
        Assert.Equal("https://custom.openrouter.ai/v1", config.BaseUrl);
    }

    [Fact]
    public void Create_WithOpenRouterTypeIsCaseInsensitive()
    {
        var options = new LoopOptions
        {
            ProviderType = "OpenRouter"
        };

        var config = ProviderConfigFactory.Create(options);

        Assert.NotNull(config);
        Assert.Equal("OpenRouter", config.Type);
        Assert.Equal("https://openrouter.ai/api/v1", config.BaseUrl);
    }

    [Fact]
    public void Create_WithOpenRouterType_FallsBackToEnvVar()
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "sk-or-from-env");
        try
        {
            var options = new LoopOptions { ProviderType = "openrouter" };

            var config = ProviderConfigFactory.Create(options);

            Assert.NotNull(config);
            Assert.Equal("sk-or-from-env", config.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        }
    }

    [Fact]
    public void Create_WithOpenRouterType_ExplicitKeyTakesPrecedenceOverEnvVar()
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "sk-or-from-env");
        try
        {
            var options = new LoopOptions
            {
                ProviderType = "openrouter",
                ProviderApiKey = "sk-or-explicit"
            };

            var config = ProviderConfigFactory.Create(options);

            Assert.NotNull(config);
            Assert.Equal("sk-or-explicit", config.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        }
    }
}
