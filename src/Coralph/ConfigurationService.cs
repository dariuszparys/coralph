using Microsoft.Extensions.Configuration;

namespace Coralph;

/// <summary>
/// Service responsible for loading and resolving configuration options.
/// Consolidates all configuration-related logic in compliance with SRP.
/// </summary>
internal static class ConfigurationService
{
    /// <summary>
    /// Loads configuration from file (if exists) and applies CLI overrides.
    /// </summary>
    internal static LoopOptions LoadOptions(LoopOptionsOverrides overrides, string? configFile)
    {
        var path = ResolveConfigPath(configFile);
        var options = new LoopOptions();

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(path, optional: true, reloadOnChange: false)
                .Build();

            config.GetSection(LoopOptions.ConfigurationSectionName).Bind(options);
        }

        ApplyOverrides(options, overrides);
        return options;
    }

    /// <summary>
    /// Resolves the full path to the configuration file.
    /// </summary>
    internal static string ResolveConfigPath(string? configFile)
    {
        var path = configFile ?? LoopOptions.ConfigurationFileName;
        if (Path.IsPathRooted(path))
            return path;

        if (configFile is null)
        {
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (File.Exists(cwdPath))
                return cwdPath;

            return Path.Combine(AppContext.BaseDirectory, path);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    /// <summary>
    /// Applies CLI argument overrides to the loaded configuration options.
    /// </summary>
    internal static void ApplyOverrides(LoopOptions target, LoopOptionsOverrides overrides)
    {
        if (overrides.MaxIterations is { } max) target.MaxIterations = max;
        if (!string.IsNullOrWhiteSpace(overrides.Model)) target.Model = overrides.Model;
        if (!string.IsNullOrWhiteSpace(overrides.ProviderType)) target.ProviderType = overrides.ProviderType;
        if (!string.IsNullOrWhiteSpace(overrides.ProviderBaseUrl)) target.ProviderBaseUrl = overrides.ProviderBaseUrl;
        if (!string.IsNullOrWhiteSpace(overrides.ProviderWireApi)) target.ProviderWireApi = overrides.ProviderWireApi;
        if (!string.IsNullOrWhiteSpace(overrides.ProviderApiKey)) target.ProviderApiKey = overrides.ProviderApiKey;
        if (!string.IsNullOrWhiteSpace(overrides.PromptFile)) target.PromptFile = overrides.PromptFile;
        if (!string.IsNullOrWhiteSpace(overrides.ProgressFile)) target.ProgressFile = overrides.ProgressFile;
        if (!string.IsNullOrWhiteSpace(overrides.IssuesFile)) target.IssuesFile = overrides.IssuesFile;
        if (!string.IsNullOrWhiteSpace(overrides.GeneratedTasksFile)) target.GeneratedTasksFile = overrides.GeneratedTasksFile;
        if (overrides.RefreshIssues is { } refresh) target.RefreshIssues = refresh;
        if (!string.IsNullOrWhiteSpace(overrides.Repo)) target.Repo = overrides.Repo;
        if (overrides.RefreshIssuesAzdo is { } refreshAzdo) target.RefreshIssuesAzdo = refreshAzdo;
        if (!string.IsNullOrWhiteSpace(overrides.AzdoOrganization)) target.AzdoOrganization = overrides.AzdoOrganization;
        if (!string.IsNullOrWhiteSpace(overrides.AzdoProject)) target.AzdoProject = overrides.AzdoProject;
        if (!string.IsNullOrWhiteSpace(overrides.CliPath)) target.CliPath = overrides.CliPath;
        if (!string.IsNullOrWhiteSpace(overrides.CliUrl)) target.CliUrl = overrides.CliUrl;
        if (!string.IsNullOrWhiteSpace(overrides.CopilotConfigPath)) target.CopilotConfigPath = overrides.CopilotConfigPath;
        if (!string.IsNullOrWhiteSpace(overrides.CopilotToken)) target.CopilotToken = overrides.CopilotToken;
        if (overrides.ToolAllow is not null) target.ToolAllow = overrides.ToolAllow;
        if (overrides.ToolDeny is not null) target.ToolDeny = overrides.ToolDeny;
        if (overrides.ShowReasoning is { } showReasoning) target.ShowReasoning = showReasoning;
        if (overrides.ColorizedOutput is { } colorizedOutput) target.ColorizedOutput = colorizedOutput;
        if (overrides.StreamEvents is { } streamEvents) target.StreamEvents = streamEvents;
        if (overrides.DockerSandbox is { } dockerSandbox) target.DockerSandbox = dockerSandbox;
        if (!string.IsNullOrWhiteSpace(overrides.DockerImage)) target.DockerImage = overrides.DockerImage;
        if (overrides.ListModels is { } listModels) target.ListModels = listModels;
        if (overrides.ListModelsJson is { } listModelsJson) target.ListModelsJson = listModelsJson;
    }
}
