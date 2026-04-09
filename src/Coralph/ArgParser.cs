using System.CommandLine;
using System.CommandLine.Help;
using Coralph.Ui;

namespace Coralph;

internal static class ArgParser
{
    internal static (LoopOptionsOverrides? Overrides, string? Error, bool Init, string? ConfigFile, bool ShowHelp, bool ShowVersion) Parse(string[] args)
    {
        var options = new LoopOptionsOverrides();
        var optionSet = CreateOptionSet();
        string? configFile = null;
        var showHelp = false;
        var showVersion = false;
        var init = false;
        var errorMessages = new List<string>();

        var root = optionSet.CreateRootCommand();
        var result = root.Parse(args);
        showHelp = result.GetValueForOption(optionSet.Help);
        showVersion = result.GetValueForOption(optionSet.Version);
        init = result.GetValueForOption(optionSet.Init);
        configFile = result.GetValueForOption(optionSet.Config);

        ApplyRequiredStringOption(result, optionSet.WorkingDir, value => options.WorkingDir = value, errorMessages, "--working-dir is required");

        var maxIterations = result.GetValueForOption(optionSet.MaxIterations);
        if (maxIterations is { } parsedMaxIterations)
        {
            if (parsedMaxIterations < 1)
            {
                errorMessages.Add("--max-iterations must be an integer >= 1");
            }
            else
            {
                options.MaxIterations = parsedMaxIterations;
            }
        }

        ApplyRequiredStringOption(result, optionSet.Model, value => options.Model = value, errorMessages, "--model is required");
        ApplyRequiredStringOption(result, optionSet.ProviderType, value => options.ProviderType = value, errorMessages, "--provider-type is required");
        ApplyRequiredStringOption(result, optionSet.ProviderBaseUrl, value => options.ProviderBaseUrl = value, errorMessages, "--provider-base-url is required");
        ApplyRequiredStringOption(result, optionSet.ProviderWireApi, value => options.ProviderWireApi = value, errorMessages, "--provider-wire-api is required");
        ApplyRequiredStringOption(result, optionSet.ProviderApiKey, value => options.ProviderApiKey = value, errorMessages, "--provider-api-key is required");
        ApplyRequiredStringOption(result, optionSet.PromptFile, value => options.PromptFile = value, errorMessages, "--prompt-file is required");
        ApplyRequiredStringOption(result, optionSet.ProgressFile, value => options.ProgressFile = value, errorMessages, "--progress-file is required");
        ApplyRequiredStringOption(result, optionSet.IssuesFile, value => options.IssuesFile = value, errorMessages, "--issues-file is required");
        ApplyRequiredStringOption(result, optionSet.GeneratedTasksFile, value => options.GeneratedTasksFile = value, errorMessages, "--generated-tasks-file is required");

        if (result.GetValueForOption(optionSet.RefreshIssues))
        {
            options.RefreshIssues = true;
        }

        var repo = result.GetValueForOption(optionSet.Repo);
        if (repo is not null)
        {
            ApplyRequiredStringOption(result, optionSet.Repo, value => options.Repo = value, errorMessages, "--repo is required");
        }

        if (result.GetValueForOption(optionSet.RefreshIssuesAzdo))
        {
            options.RefreshIssuesAzdo = true;
        }

        var azdoOrganization = result.GetValueForOption(optionSet.AzdoOrganization);
        if (azdoOrganization is not null)
        {
            options.AzdoOrganization = azdoOrganization;
        }

        var azdoProject = result.GetValueForOption(optionSet.AzdoProject);
        if (azdoProject is not null)
        {
            options.AzdoProject = azdoProject;
        }

        var showReasoning = result.GetValueForOption(optionSet.ShowReasoning);
        if (showReasoning.HasValue)
        {
            options.ShowReasoning = showReasoning.Value;
        }

        var colorizedOutput = result.GetValueForOption(optionSet.ColorizedOutput);
        if (colorizedOutput.HasValue)
        {
            options.ColorizedOutput = colorizedOutput.Value;
        }

        var ui = result.GetValueForOption(optionSet.Ui);
        if (ui is not null)
        {
            if (!UiModeParser.TryParse(ui, out var uiMode))
            {
                errorMessages.Add($"--ui must be one of: {UiModeParser.HelpText}");
            }
            else
            {
                options.UiMode = uiMode;
            }
        }

        if (result.GetValueForOption(optionSet.Demo))
        {
            options.DemoMode = true;
        }

        var streamEvents = result.GetValueForOption(optionSet.StreamEvents);
        if (streamEvents.HasValue)
        {
            options.StreamEvents = streamEvents.Value;
        }

        ApplyRequiredStringOption(result, optionSet.CliPath, value => options.CliPath = value, errorMessages, "--cli-path is required");
        ApplyRequiredStringOption(result, optionSet.CliUrl, value => options.CliUrl = value, errorMessages, "--cli-url is required");
        ApplyRequiredStringOption(result, optionSet.CopilotConfigPath, value => options.CopilotConfigPath = value, errorMessages, "--copilot-config-path is required");
        ApplyRequiredStringOption(result, optionSet.CopilotToken, value => options.CopilotToken = value, errorMessages, "--copilot-token is required");

        var toolAllow = result.GetValueForOption(optionSet.ToolAllow);
        if (toolAllow is { Length: > 0 })
        {
            var normalized = NormalizeMultiValueOption(toolAllow);
            if (normalized.Length > 0)
            {
                options.ToolAllow = normalized;
            }
        }

        var toolDeny = result.GetValueForOption(optionSet.ToolDeny);
        if (toolDeny is { Length: > 0 })
        {
            var normalized = NormalizeMultiValueOption(toolDeny);
            if (normalized.Length > 0)
            {
                options.ToolDeny = normalized;
            }
        }

        var dockerSandbox = result.GetValueForOption(optionSet.DockerSandbox);
        if (dockerSandbox.HasValue)
        {
            options.DockerSandbox = dockerSandbox.Value;
        }

        ApplyRequiredStringOption(result, optionSet.DockerImage, value => options.DockerImage = value, errorMessages, "--docker-image is required");
        ApplyRequiredStringOption(result, optionSet.DockerNetworkMode, value => options.DockerNetworkMode = value, errorMessages, "--docker-network must not be empty");
        ApplyRequiredStringOption(result, optionSet.DockerMemoryLimit, value => options.DockerMemoryLimit = value, errorMessages, "--docker-memory must not be empty");
        ApplyRequiredStringOption(result, optionSet.DockerCpuLimit, value => options.DockerCpuLimit = value, errorMessages, "--docker-cpus must not be empty");

        if (result.GetValueForOption(optionSet.ListModels))
        {
            options.ListModels = true;
        }

        if (result.GetValueForOption(optionSet.ListModelsJson))
        {
            options.ListModelsJson = true;
            options.ListModels = true;
        }

        ApplyRequiredStringOption(result, optionSet.ClientName, value => options.ClientName = value, errorMessages, "--client-name must not be empty");

        var reasoningEffort = result.GetValueForOption(optionSet.ReasoningEffort);
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            options.ReasoningEffort = reasoningEffort.Trim();
        }

        ApplyRequiredStringOption(result, optionSet.TelemetryOtlpEndpoint, value => options.TelemetryOtlpEndpoint = value, errorMessages, "--telemetry-otlp-endpoint must not be empty");
        ApplyRequiredStringOption(result, optionSet.TelemetrySourceName, value => options.TelemetrySourceName = value, errorMessages, "--telemetry-source-name must not be empty");

        var telemetryCaptureContent = result.GetValueForOption(optionSet.TelemetryCaptureContent);
        if (telemetryCaptureContent.HasValue)
        {
            options.TelemetryCaptureContent = telemetryCaptureContent.Value;
        }

        if (result.GetValueForOption(optionSet.DryRun))
        {
            options.DryRun = true;
        }

        if (result.Errors.Count > 0)
        {
            errorMessages.AddRange(result.Errors.Select(e => e.Message));
        }

        if (showHelp)
        {
            return (null, null, init, configFile, true, false);
        }

        if (showVersion)
        {
            return (null, null, init, configFile, false, true);
        }

        if (errorMessages.Count > 0)
        {
            return (null, string.Join(Environment.NewLine, errorMessages), init, configFile, false, false);
        }

        return (options, null, init, configFile, false, false);
    }

    internal static void PrintUsage(TextWriter w)
    {
        var root = BuildRootCommand();
        var helpBuilder = new HelpBuilder(LocalizationResources.Instance);
        w.WriteLine("Coralph - Ralph loop runner using GitHub Copilot SDK");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  dotnet run --project src/Coralph -- [options]");
        w.WriteLine();
        helpBuilder.Write(root, w);
    }

    private static RootCommand BuildRootCommand()
    {
        var optionSet = CreateOptionSet();
        var root = new RootCommand("Coralph - Ralph loop runner using GitHub Copilot SDK");
        optionSet.AddTo(root);
        return root;
    }

    internal static IReadOnlyList<string> GetRegisteredOptionNames()
    {
        return CreateOptionSet().RegisteredOptionNames;
    }

    private static OptionSet CreateOptionSet()
    {
        return new OptionSet();
    }

    private static void ApplyRequiredStringOption(
        System.CommandLine.Parsing.ParseResult result,
        Option<string?> option,
        Action<string> assign,
        List<string> errorMessages,
        string errorMessage)
    {
        var value = result.GetValueForOption(option);
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessages.Add(errorMessage);
            return;
        }

        assign(value);
    }

    private static string[] NormalizeMultiValueOption(IEnumerable<string> values)
    {
        var results = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    results.Add(token);
                }
            }
        }

        return results.ToArray();
    }

    private sealed class OptionSet
    {
        internal OptionSet()
        {
            Help = new Option<bool>(new[] { "-h", "--help" }, "Show help");
            Version = new Option<bool>(new[] { "-v", "--version" }, "Show version");
            WorkingDir = new Option<string?>("--working-dir", "Path to target git repository (run Coralph as if launched there)");
            MaxIterations = new Option<int?>("--max-iterations", "Max loop iterations (default: 10)");
            Model = new Option<string?>("--model", "Model (default: GPT-5.1-Codex)");
            ProviderType = new Option<string?>("--provider-type", "Optional: provider type (e.g. openai, openrouter)");
            ProviderBaseUrl = new Option<string?>("--provider-base-url", "Optional: provider base URL (e.g. https://api.openai.com/v1/)");
            ProviderWireApi = new Option<string?>("--provider-wire-api", "Optional: provider wire API (e.g. responses)");
            ProviderApiKey = new Option<string?>("--provider-api-key", "Optional: provider API key (e.g. for openrouter: sk-or-...)");
            PromptFile = new Option<string?>("--prompt-file", "Prompt file (default: prompt.md)");
            ProgressFile = new Option<string?>("--progress-file", "Progress file (default: progress.txt)");
            IssuesFile = new Option<string?>("--issues-file", "Issues json file (default: issues.json)");
            GeneratedTasksFile = new Option<string?>("--generated-tasks-file", "Generated tasks backlog file (default: generated_tasks.json)");
            RefreshIssues = new Option<bool>("--refresh-issues", "Refresh issues.json via `gh issue list`");
            Repo = new Option<string?>("--repo", "Optional repo override for gh");
            RefreshIssuesAzdo = new Option<bool>("--refresh-issues-azdo", "Refresh issues.json from Azure Boards via `az boards`");
            AzdoOrganization = new Option<string?>("--azdo-organization", "Azure DevOps organization URL (uses az devops defaults if not set)");
            AzdoProject = new Option<string?>("--azdo-project", "Azure DevOps project name (uses az devops defaults if not set)");
            CliPath = new Option<string?>("--cli-path", "Optional: Copilot CLI executable path");
            CliUrl = new Option<string?>("--cli-url", "Optional: connect to existing CLI server");
            CopilotConfigPath = new Option<string?>("--copilot-config-path", "Optional: Copilot CLI config directory to mount into Docker sandbox");
            CopilotToken = new Option<string?>("--copilot-token", "Optional: GitHub token for non-interactive Copilot CLI auth (sets GH_TOKEN)");
            ToolAllow = new Option<string[]>("--tool-allow", "Allow listed tool/permission kinds. Dangerous tools are denied by default and can be explicitly opted back in here; unspecified tools are denied when this list is non-empty (repeatable or comma-separated)")
            {
                AllowMultipleArgumentsPerToken = true
            };
            ToolDeny = new Option<string[]>("--tool-deny", "Deny listed tool/permission kinds. User deny rules override both explicit allows and the built-in dangerous-tool defaults (repeatable or comma-separated)")
            {
                AllowMultipleArgumentsPerToken = true
            };
            Config = new Option<string?>("--config", "Optional: JSON config file (default: coralph.config.json)");
            Init = new Option<bool>("--init", "Initialize the repository (issues.json, config, prompt, progress) and exits");
            ShowReasoning = new Option<bool?>("--show-reasoning", "Show reasoning output (default: true)");
            ColorizedOutput = new Option<bool?>("--colorized-output", "Use colored output (default: true)");
            Ui = new Option<string?>("--ui", $"UI mode ({UiModeParser.HelpText}, default: auto)");
            Demo = new Option<bool>("--demo", "Run in demo mode with mock UI data");
            StreamEvents = new Option<bool?>(new[] { "--stream-events", "--event-stream" }, "Emit structured JSON events to stdout");
            DockerSandbox = new Option<bool?>("--docker-sandbox", "Run each iteration inside a Docker container (default: false)");
            DockerImage = new Option<string?>("--docker-image", "Docker image for sandbox (default: mcr.microsoft.com/devcontainers/dotnet:10.0)");
            DockerNetworkMode = new Option<string?>("--docker-network", "Docker network mode for sandbox (default: none)");
            DockerMemoryLimit = new Option<string?>("--docker-memory", "Docker memory limit for sandbox (default: 2g)");
            DockerCpuLimit = new Option<string?>("--docker-cpus", "Docker CPU limit for sandbox (default: 2)");
            ListModels = new Option<bool>("--list-models", "List available Copilot models and exit");
            ListModelsJson = new Option<bool>("--list-models-json", "List available Copilot models as JSON and exit");
            ClientName = new Option<string?>("--client-name", "Client name sent to Copilot session (default: coralph)");
            ReasoningEffort = new Option<string?>("--reasoning-effort", "Reasoning effort hint for the model (e.g. low, medium, high); omit to use model default");
            TelemetryOtlpEndpoint = new Option<string?>("--telemetry-otlp-endpoint", "Optional: OTLP HTTP endpoint for Copilot SDK telemetry (e.g. http://localhost:4318)");
            TelemetrySourceName = new Option<string?>("--telemetry-source-name", "Optional: source name for Copilot SDK telemetry spans");
            TelemetryCaptureContent = new Option<bool?>("--telemetry-capture-content", "Optional: include prompt/response content in Copilot SDK telemetry");
            DryRun = new Option<bool>("--dry-run", "Execute the full loop but prevent any actual file writes or git commits (preview mode)");
        }

        internal Option<bool> Help { get; }
        internal Option<bool> Version { get; }
        internal Option<string?> WorkingDir { get; }
        internal Option<int?> MaxIterations { get; }
        internal Option<string?> Model { get; }
        internal Option<string?> ProviderType { get; }
        internal Option<string?> ProviderBaseUrl { get; }
        internal Option<string?> ProviderWireApi { get; }
        internal Option<string?> ProviderApiKey { get; }
        internal Option<string?> PromptFile { get; }
        internal Option<string?> ProgressFile { get; }
        internal Option<string?> IssuesFile { get; }
        internal Option<string?> GeneratedTasksFile { get; }
        internal Option<bool> RefreshIssues { get; }
        internal Option<string?> Repo { get; }
        internal Option<bool> RefreshIssuesAzdo { get; }
        internal Option<string?> AzdoOrganization { get; }
        internal Option<string?> AzdoProject { get; }
        internal Option<string?> CliPath { get; }
        internal Option<string?> CliUrl { get; }
        internal Option<string?> CopilotConfigPath { get; }
        internal Option<string?> CopilotToken { get; }
        internal Option<string[]> ToolAllow { get; }
        internal Option<string[]> ToolDeny { get; }
        internal Option<string?> Config { get; }
        internal Option<bool> Init { get; }
        internal Option<bool?> ShowReasoning { get; }
        internal Option<bool?> ColorizedOutput { get; }
        internal Option<string?> Ui { get; }
        internal Option<bool> Demo { get; }
        internal Option<bool?> StreamEvents { get; }
        internal Option<bool?> DockerSandbox { get; }
        internal Option<string?> DockerImage { get; }
        internal Option<string?> DockerNetworkMode { get; }
        internal Option<string?> DockerMemoryLimit { get; }
        internal Option<string?> DockerCpuLimit { get; }
        internal Option<bool> ListModels { get; }
        internal Option<bool> ListModelsJson { get; }
        internal Option<string?> ClientName { get; }
        internal Option<string?> ReasoningEffort { get; }
        internal Option<string?> TelemetryOtlpEndpoint { get; }
        internal Option<string?> TelemetrySourceName { get; }
        internal Option<bool?> TelemetryCaptureContent { get; }
        internal Option<bool> DryRun { get; }

        internal IReadOnlyList<Option> AllOptions =>
        [
            Help,
            Version,
            WorkingDir,
            MaxIterations,
            Model,
            ProviderType,
            ProviderBaseUrl,
            ProviderWireApi,
            ProviderApiKey,
            PromptFile,
            ProgressFile,
            IssuesFile,
            GeneratedTasksFile,
            RefreshIssues,
            Repo,
            RefreshIssuesAzdo,
            AzdoOrganization,
            AzdoProject,
            CliPath,
            CliUrl,
            CopilotConfigPath,
            CopilotToken,
            ToolAllow,
            ToolDeny,
            Config,
            Init,
            ShowReasoning,
            ColorizedOutput,
            Ui,
            Demo,
            StreamEvents,
            DockerSandbox,
            DockerImage,
            DockerNetworkMode,
            DockerMemoryLimit,
            DockerCpuLimit,
            ListModels,
            ListModelsJson,
            ClientName,
            ReasoningEffort,
            TelemetryOtlpEndpoint,
            TelemetrySourceName,
            TelemetryCaptureContent,
            DryRun
        ];

        internal IReadOnlyList<string> RegisteredOptionNames =>
            AllOptions.SelectMany(option => option.Aliases).Distinct(StringComparer.Ordinal).ToArray();

        internal RootCommand CreateRootCommand()
        {
            var root = new RootCommand("Coralph - Ralph loop runner using GitHub Copilot SDK");
            AddTo(root);
            return root;
        }

        internal void AddTo(RootCommand root)
        {
            foreach (var option in AllOptions)
            {
                root.AddOption(option);
            }
        }
    }
}
