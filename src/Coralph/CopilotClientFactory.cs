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
            Telemetry = CreateTelemetryConfig(options),
            Logger = CopilotSdkLogger.Instance,
            LogLevel = CreateCopilotLogLevel(options.CopilotLogLevel)
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
        Action<SessionEvent>? onEvent = null,
        EventStreamWriter? eventStream = null)
    {
        var modeSwitchHandlers = new CopilotModeSwitchHandlers(options, eventStream);

        return new SessionConfig
        {
            Model = options.Model,
            Streaming = true,
            Tools = tools,
            OnPermissionRequest = onPermissionRequest,
            OnEvent = onEvent,
            OnAutoModeSwitchRequest = modeSwitchHandlers.HandleAutoModeSwitchRequestAsync,
            OnExitPlanModeRequest = modeSwitchHandlers.HandleExitPlanModeRequestAsync,
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

    private static CopilotLogLevel? CreateCopilotLogLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "all" => CopilotLogLevel.All,
            "debug" => CopilotLogLevel.Debug,
            "error" => CopilotLogLevel.Error,
            "info" or "information" => CopilotLogLevel.Info,
            "none" => CopilotLogLevel.None,
            "warn" or "warning" => CopilotLogLevel.Warning,
            var custom => new CopilotLogLevel(custom)
        };
    }
}
