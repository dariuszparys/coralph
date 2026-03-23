using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace Coralph;

internal static class CopilotClientFactory
{
    internal static CopilotClientOptions CreateClientOptions(LoopOptions options)
    {
        var clientOptions = new CopilotClientOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
        };

        if (!string.IsNullOrWhiteSpace(options.CliPath))
        {
            clientOptions.CliPath = options.CliPath;
        }

        if (!string.IsNullOrWhiteSpace(options.CliUrl))
        {
            clientOptions.CliUrl = options.CliUrl;
        }

        if (!string.IsNullOrWhiteSpace(options.CopilotToken))
        {
            clientOptions.GitHubToken = options.CopilotToken;
        }

        return clientOptions;
    }

    internal static SessionConfig CreateSessionConfig(
        LoopOptions options,
        AIFunction[] tools,
        PermissionRequestHandler onPermissionRequest)
    {
        return new SessionConfig
        {
            Model = options.Model,
            Streaming = true,
            Tools = tools,
            OnPermissionRequest = onPermissionRequest,
            Provider = ProviderConfigFactory.Create(options),
            ClientName = options.ClientName,
            ReasoningEffort = options.ReasoningEffort
        };
    }
}
