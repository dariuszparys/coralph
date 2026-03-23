using Microsoft.Extensions.Configuration;

namespace Coralph;

internal static class ConfigurationService
{
    internal static LoopOptions LoadOptions(LoopOptionsOverrides cliOverrides, string? configFile)
    {
        ArgumentNullException.ThrowIfNull(cliOverrides);

        var configOverrides = new LoopOptionsOverrides();
        var path = ResolveConfigPath(configFile);

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(path, optional: true, reloadOnChange: false)
                .Build();

            config.GetSection(LoopOptions.ConfigurationSectionName).Bind(configOverrides);
        }

        return Merge(cliOverrides, configOverrides);
    }

    internal static string ResolveConfigPath(string? configFile)
    {
        var path = configFile ?? LoopOptions.ConfigurationFileName;
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        if (configFile is null)
        {
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (File.Exists(cwdPath))
            {
                return cwdPath;
            }

            return Path.Combine(AppContext.BaseDirectory, path);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    internal static LoopOptions Merge(LoopOptionsOverrides cli, LoopOptionsOverrides config)
    {
        ArgumentNullException.ThrowIfNull(cli);
        ArgumentNullException.ThrowIfNull(config);
        var defaults = new LoopOptions();

        return new LoopOptions
        {
            MaxIterations = cli.MaxIterations ?? config.MaxIterations ?? defaults.MaxIterations,
            Model = Coalesce(cli.Model, config.Model, defaults.Model),
            ProviderType = CoalesceNullable(cli.ProviderType, config.ProviderType),
            ProviderBaseUrl = CoalesceNullable(cli.ProviderBaseUrl, config.ProviderBaseUrl),
            ProviderWireApi = CoalesceNullable(cli.ProviderWireApi, config.ProviderWireApi),
            ProviderApiKey = CoalesceNullable(cli.ProviderApiKey, config.ProviderApiKey),
            PromptFile = Coalesce(cli.PromptFile, config.PromptFile, defaults.PromptFile),
            ProgressFile = Coalesce(cli.ProgressFile, config.ProgressFile, defaults.ProgressFile),
            IssuesFile = Coalesce(cli.IssuesFile, config.IssuesFile, defaults.IssuesFile),
            GeneratedTasksFile = Coalesce(cli.GeneratedTasksFile, config.GeneratedTasksFile, defaults.GeneratedTasksFile),
            RefreshIssues = cli.RefreshIssues ?? config.RefreshIssues ?? defaults.RefreshIssues,
            Repo = CoalesceNullable(cli.Repo, config.Repo),
            RefreshIssuesAzdo = cli.RefreshIssuesAzdo ?? config.RefreshIssuesAzdo ?? defaults.RefreshIssuesAzdo,
            AzdoOrganization = CoalesceNullable(cli.AzdoOrganization, config.AzdoOrganization),
            AzdoProject = CoalesceNullable(cli.AzdoProject, config.AzdoProject),
            CliPath = CoalesceNullable(cli.CliPath, config.CliPath),
            CliUrl = CoalesceNullable(cli.CliUrl, config.CliUrl),
            CopilotConfigPath = CoalesceNullable(cli.CopilotConfigPath, config.CopilotConfigPath),
            CopilotToken = CoalesceNullable(cli.CopilotToken, config.CopilotToken),
            ToolAllow = cli.ToolAllow ?? config.ToolAllow ?? [],
            ToolDeny = cli.ToolDeny ?? config.ToolDeny ?? [],
            ShowReasoning = cli.ShowReasoning ?? config.ShowReasoning ?? defaults.ShowReasoning,
            ColorizedOutput = cli.ColorizedOutput ?? config.ColorizedOutput ?? defaults.ColorizedOutput,
            UiMode = cli.UiMode ?? config.UiMode ?? defaults.UiMode,
            StreamEvents = cli.StreamEvents ?? config.StreamEvents ?? defaults.StreamEvents,
            DockerSandbox = cli.DockerSandbox ?? config.DockerSandbox ?? defaults.DockerSandbox,
            DockerImage = Coalesce(cli.DockerImage, config.DockerImage, defaults.DockerImage),
            DockerNetworkMode = Coalesce(cli.DockerNetworkMode, config.DockerNetworkMode, defaults.DockerNetworkMode),
            DockerMemoryLimit = Coalesce(cli.DockerMemoryLimit, config.DockerMemoryLimit, defaults.DockerMemoryLimit),
            DockerCpuLimit = Coalesce(cli.DockerCpuLimit, config.DockerCpuLimit, defaults.DockerCpuLimit),
            ListModels = cli.ListModels ?? config.ListModels ?? defaults.ListModels,
            ListModelsJson = cli.ListModelsJson ?? config.ListModelsJson ?? defaults.ListModelsJson,
            DemoMode = cli.DemoMode ?? config.DemoMode ?? defaults.DemoMode,
            ClientName = Coalesce(cli.ClientName, config.ClientName, defaults.ClientName),
            ReasoningEffort = CoalesceNullable(cli.ReasoningEffort, config.ReasoningEffort),
            TelemetryOtlpEndpoint = CoalesceNullable(cli.TelemetryOtlpEndpoint, config.TelemetryOtlpEndpoint),
            TelemetrySourceName = CoalesceNullable(cli.TelemetrySourceName, config.TelemetrySourceName),
            TelemetryCaptureContent = cli.TelemetryCaptureContent ?? config.TelemetryCaptureContent ?? defaults.TelemetryCaptureContent,
            DryRun = cli.DryRun ?? config.DryRun ?? defaults.DryRun
        };
    }

    private static string Coalesce(string? cliValue, string? configValue, string fallback)
    {
        return !string.IsNullOrWhiteSpace(cliValue)
            ? cliValue
            : !string.IsNullOrWhiteSpace(configValue)
                ? configValue
                : fallback;
    }

    private static string? CoalesceNullable(string? cliValue, string? configValue)
    {
        return !string.IsNullOrWhiteSpace(cliValue)
            ? cliValue
            : !string.IsNullOrWhiteSpace(configValue)
                ? configValue
                : null;
    }
}
