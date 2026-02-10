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
}
