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
            Telemetry = CreateTelemetryConfig(options)
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
        PermissionRequestHandler onPermissionRequest,
        SessionEventHandler? onEvent = null)
    {
        return new SessionConfig
        {
            Model = options.Model,
            Streaming = true,
            Tools = tools,
            OnPermissionRequest = onPermissionRequest,
            OnEvent = onEvent,
            Provider = ProviderConfigFactory.Create(options),
            ClientName = options.ClientName,
            ReasoningEffort = options.ReasoningEffort,
            SystemMessage = CopilotSystemMessageFactory.Create(options)
        };
    }

    private static TelemetryConfig? CreateTelemetryConfig(LoopOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TelemetryOtlpEndpoint) &&
            string.IsNullOrWhiteSpace(options.TelemetrySourceName) &&
            !options.TelemetryCaptureContent.HasValue)
        {
            return null;
        }

        return new TelemetryConfig
        {
            OtlpEndpoint = options.TelemetryOtlpEndpoint,
            SourceName = options.TelemetrySourceName,
            CaptureContent = options.TelemetryCaptureContent
        };
    }
}
