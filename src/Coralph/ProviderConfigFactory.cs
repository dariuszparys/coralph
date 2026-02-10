using GitHub.Copilot.SDK;

namespace Coralph;

internal static class ProviderConfigFactory
{
    internal static ProviderConfig? Create(LoopOptions options)
    {
        var hasType = !string.IsNullOrWhiteSpace(options.ProviderType);
        var hasBaseUrl = !string.IsNullOrWhiteSpace(options.ProviderBaseUrl);
        var hasWireApi = !string.IsNullOrWhiteSpace(options.ProviderWireApi);
        var hasApiKey = !string.IsNullOrWhiteSpace(options.ProviderApiKey);

        if (!hasType && !hasBaseUrl && !hasWireApi && !hasApiKey)
        {
            return null;
        }

        var type = options.ProviderType;
        if (string.IsNullOrWhiteSpace(type))
        {
            type = "openai";
        }

        var baseUrl = options.ProviderBaseUrl;
        var wireApi = options.ProviderWireApi;

        if (string.Equals(type, "openai", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "https://api.openai.com/v1/";
            }

            if (string.IsNullOrWhiteSpace(wireApi))
            {
                wireApi = "responses";
            }
        }

        return new ProviderConfig
        {
            Type = type,
            BaseUrl = baseUrl,
            WireApi = wireApi,
            ApiKey = options.ProviderApiKey
        };
    }
}
