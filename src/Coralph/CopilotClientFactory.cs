using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;

#pragma warning disable GHCP001

namespace Coralph;

internal static class CopilotClientFactory
{
    internal static CopilotClientOptions CreateClientOptions(LoopOptions options)
    {
        var clientOptions = new CopilotClientOptions
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Telemetry = CreateTelemetryConfig(options)
        };

        if (!string.IsNullOrWhiteSpace(options.CliPath))
        {
            clientOptions.Connection = RuntimeConnection.ForStdio(options.CliPath, args: null);
        }

        if (!string.IsNullOrWhiteSpace(options.CliUrl))
        {
            clientOptions.Connection = RuntimeConnection.ForUri(options.CliUrl, connectionToken: null);
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
        Func<PermissionRequest, PermissionInvocation, Task<PermissionDecision>> onPermissionRequest,
        Action<SessionEvent>? onEvent = null)
    {
        return new SessionConfig
        {
            Model = options.Model,
            Streaming = true,
            Tools = tools,
            OnPermissionRequest = onPermissionRequest,
            OnEvent = onEvent,
            Provider = ProviderConfigFactory.Create(options),
            GitHubToken = string.IsNullOrWhiteSpace(options.CopilotToken) ? null : options.CopilotToken,
            IncludeSubAgentStreamingEvents = true,
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
