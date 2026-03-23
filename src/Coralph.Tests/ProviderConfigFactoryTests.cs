using Coralph;

namespace Coralph.Tests;

[CollectionDefinition("EnvironmentVariables", DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection : ICollectionFixture<EnvironmentVariablesFixture>
{
}

public sealed class EnvironmentVariablesFixture : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}

[Collection("EnvironmentVariables")]
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
        WithEnvironmentVariable("OPENROUTER_API_KEY", "sk-or-from-env", () =>
        {
            var options = new LoopOptions { ProviderType = "openrouter" };

            var config = ProviderConfigFactory.Create(options);

            Assert.NotNull(config);
            Assert.Equal("sk-or-from-env", config.ApiKey);
        });
    }

    [Fact]
    public void Create_WithOpenRouterType_ExplicitKeyTakesPrecedenceOverEnvVar()
    {
        WithEnvironmentVariable("OPENROUTER_API_KEY", "sk-or-from-env", () =>
        {
            var options = new LoopOptions
            {
                ProviderType = "openrouter",
                ProviderApiKey = "sk-or-explicit"
            };

            var config = ProviderConfigFactory.Create(options);

            Assert.NotNull(config);
            Assert.Equal("sk-or-explicit", config.ApiKey);
        });
    }

    [Fact]
    public void Create_WithCoralphProviderApiKeyEnvVar_UsesGenericFallback()
    {
        WithEnvironmentVariable("CORALPH_PROVIDER_API_KEY", "sk-generic-from-env", () =>
        {
            var options = new LoopOptions
            {
                ProviderType = "openai"
            };

            var config = ProviderConfigFactory.Create(options);

            Assert.NotNull(config);
            Assert.Equal("sk-generic-from-env", config.ApiKey);
        });
    }

    private static void WithEnvironmentVariable(string name, string? value, Action assertion)
    {
        var original = Environment.GetEnvironmentVariable(name);

        try
        {
            Environment.SetEnvironmentVariable(name, value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }
}
