using GitHub.Copilot.SDK;

namespace Coralph;

internal static class ProviderConfigFactory
{
    internal static ProviderConfig? Create(LoopOptions options)
    {
        var apiKey = options.ProviderApiKey;

        // Auto-detect OPENROUTER_API_KEY when the caller has not supplied an explicit key
        // and the provider type is (or will default to) openrouter.
        if (string.IsNullOrWhiteSpace(apiKey) &&
            string.Equals(options.ProviderType, "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        }

        var hasType = !string.IsNullOrWhiteSpace(options.ProviderType);
        var hasBaseUrl = !string.IsNullOrWhiteSpace(options.ProviderBaseUrl);
        var hasWireApi = !string.IsNullOrWhiteSpace(options.ProviderWireApi);
        var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);

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
        else if (string.Equals(type, "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "https://openrouter.ai/api/v1";
            }
        }

        return new ProviderConfig
        {
            Type = type,
            BaseUrl = baseUrl ?? string.Empty,
            WireApi = wireApi,
            ApiKey = apiKey
        };
    }
}
