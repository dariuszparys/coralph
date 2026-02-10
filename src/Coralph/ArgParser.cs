using System.CommandLine;
using System.CommandLine.Help;

namespace Coralph;

internal static class ArgParser
{
    internal static (LoopOptionsOverrides? Overrides, string? Error, bool Init, string? ConfigFile, bool ShowHelp, bool ShowVersion) Parse(string[] args)
    {
        var options = new LoopOptionsOverrides();
        string? configFile = null;
        var showHelp = false;
        var showVersion = false;
        var init = false;
        var errorMessages = new List<string>();

        var root = new RootCommand("Coralph - Ralph loop runner using GitHub Copilot SDK");
        var helpOption = new Option<bool>(new[] { "-h", "--help" }, "Show help");
        var versionOption = new Option<bool>(new[] { "-v", "--version" }, "Show version");
        var maxIterationsOption = new Option<int?>("--max-iterations", "Max loop iterations (default: 10)");
        var modelOption = new Option<string?>("--model", "Model (default: GPT-5.1-Codex)");
        var providerTypeOption = new Option<string?>("--provider-type", "Optional: provider type (e.g. openai)");
        var providerBaseUrlOption = new Option<string?>("--provider-base-url", "Optional: provider base URL (e.g. https://api.openai.com/v1/)");
        var providerWireApiOption = new Option<string?>("--provider-wire-api", "Optional: provider wire API (e.g. responses)");
        var providerApiKeyOption = new Option<string?>("--provider-api-key", "Optional: provider API key");
        var promptFileOption = new Option<string?>("--prompt-file", "Prompt file (default: prompt.md)");
        var progressFileOption = new Option<string?>("--progress-file", "Progress file (default: progress.txt)");
        var issuesFileOption = new Option<string?>("--issues-file", "Issues json file (default: issues.json)");
        var generatedTasksFileOption = new Option<string?>("--generated-tasks-file", "Generated tasks backlog file (default: generated_tasks.json)");
        var refreshIssuesOption = new Option<bool>("--refresh-issues", "Refresh issues.json via `gh issue list`");
        var repoOption = new Option<string?>("--repo", "Optional repo override for gh");
        var refreshIssuesAzdoOption = new Option<bool>("--refresh-issues-azdo", "Refresh issues.json from Azure Boards via `az boards`");
        var azdoOrganizationOption = new Option<string?>("--azdo-organization", "Azure DevOps organization URL (uses az devops defaults if not set)");
        var azdoProjectOption = new Option<string?>("--azdo-project", "Azure DevOps project name (uses az devops defaults if not set)");
        var cliPathOption = new Option<string?>("--cli-path", "Optional: Copilot CLI executable path");
        var cliUrlOption = new Option<string?>("--cli-url", "Optional: connect to existing CLI server");
        var copilotConfigPathOption = new Option<string?>("--copilot-config-path", "Optional: Copilot CLI config directory to mount into Docker sandbox");
        var copilotTokenOption = new Option<string?>("--copilot-token", "Optional: GitHub token for non-interactive Copilot CLI auth (sets GH_TOKEN)");
        var toolAllowOption = new Option<string[]>("--tool-allow", "Allow tool/permission kinds (repeatable or comma-separated)");
        var toolDenyOption = new Option<string[]>("--tool-deny", "Deny tool/permission kinds (repeatable or comma-separated)");
        var configOption = new Option<string?>("--config", "Optional: JSON config file (default: coralph.config.json)");
        var initOption = new Option<bool>("--init", "Initialize the repository (issues.json, config, prompt, progress) and exits");
        var showReasoningOption = new Option<bool?>("--show-reasoning", "Show reasoning output (default: true)");
        var colorizedOutputOption = new Option<bool?>("--colorized-output", "Use colored output (default: true)");
        var streamEventsOption = new Option<bool?>(new[] { "--stream-events", "--event-stream" }, "Emit structured JSON events to stdout");
        var dockerSandboxOption = new Option<bool?>("--docker-sandbox", "Run each iteration inside a Docker container (default: false)");
        var dockerImageOption = new Option<string?>("--docker-image", "Docker image for sandbox (default: mcr.microsoft.com/devcontainers/dotnet:10.0)");
        var listModelsOption = new Option<bool>("--list-models", "List available Copilot models and exit");
        var listModelsJsonOption = new Option<bool>("--list-models-json", "List available Copilot models as JSON and exit");

        toolAllowOption.AllowMultipleArgumentsPerToken = true;
        toolDenyOption.AllowMultipleArgumentsPerToken = true;

        root.AddOption(helpOption);
        root.AddOption(versionOption);
        root.AddOption(maxIterationsOption);
        root.AddOption(modelOption);
        root.AddOption(providerTypeOption);
        root.AddOption(providerBaseUrlOption);
        root.AddOption(providerWireApiOption);
        root.AddOption(providerApiKeyOption);
        root.AddOption(promptFileOption);
        root.AddOption(progressFileOption);
        root.AddOption(issuesFileOption);
        root.AddOption(generatedTasksFileOption);
        root.AddOption(refreshIssuesOption);
        root.AddOption(repoOption);
        root.AddOption(refreshIssuesAzdoOption);
        root.AddOption(azdoOrganizationOption);
        root.AddOption(azdoProjectOption);
        root.AddOption(cliPathOption);
        root.AddOption(cliUrlOption);
        root.AddOption(copilotConfigPathOption);
        root.AddOption(copilotTokenOption);
        root.AddOption(toolAllowOption);
        root.AddOption(toolDenyOption);
        root.AddOption(configOption);
        root.AddOption(initOption);
        root.AddOption(showReasoningOption);
        root.AddOption(colorizedOutputOption);
        root.AddOption(streamEventsOption);
        root.AddOption(dockerSandboxOption);
        root.AddOption(dockerImageOption);
        root.AddOption(listModelsOption);
        root.AddOption(listModelsJsonOption);

        var result = root.Parse(args);
        showHelp = result.GetValueForOption(helpOption);
        showVersion = result.GetValueForOption(versionOption);
        init = result.GetValueForOption(initOption);
        configFile = result.GetValueForOption(configOption);

        var maxIterations = result.GetValueForOption(maxIterationsOption);
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

        var model = result.GetValueForOption(modelOption);
        if (model is not null)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                errorMessages.Add("--model is required");
            }
            else
            {
                options.Model = model;
            }
        }

        var providerType = result.GetValueForOption(providerTypeOption);
        if (providerType is not null)
        {
            if (string.IsNullOrWhiteSpace(providerType))
            {
                errorMessages.Add("--provider-type is required");
            }
            else
            {
                options.ProviderType = providerType;
            }
        }

        var providerBaseUrl = result.GetValueForOption(providerBaseUrlOption);
        if (providerBaseUrl is not null)
        {
            if (string.IsNullOrWhiteSpace(providerBaseUrl))
            {
                errorMessages.Add("--provider-base-url is required");
            }
            else
            {
                options.ProviderBaseUrl = providerBaseUrl;
            }
        }

        var providerWireApi = result.GetValueForOption(providerWireApiOption);
        if (providerWireApi is not null)
        {
            if (string.IsNullOrWhiteSpace(providerWireApi))
            {
                errorMessages.Add("--provider-wire-api is required");
            }
            else
            {
                options.ProviderWireApi = providerWireApi;
            }
        }

        var providerApiKey = result.GetValueForOption(providerApiKeyOption);
        if (providerApiKey is not null)
        {
            if (string.IsNullOrWhiteSpace(providerApiKey))
            {
                errorMessages.Add("--provider-api-key is required");
            }
            else
            {
                options.ProviderApiKey = providerApiKey;
            }
        }

        var promptFile = result.GetValueForOption(promptFileOption);
        if (promptFile is not null)
        {
            if (string.IsNullOrWhiteSpace(promptFile))
            {
                errorMessages.Add("--prompt-file is required");
            }
            else
            {
                options.PromptFile = promptFile;
            }
        }

        var progressFile = result.GetValueForOption(progressFileOption);
        if (progressFile is not null)
        {
            if (string.IsNullOrWhiteSpace(progressFile))
            {
                errorMessages.Add("--progress-file is required");
            }
            else
            {
                options.ProgressFile = progressFile;
            }
        }

        var issuesFile = result.GetValueForOption(issuesFileOption);
        if (issuesFile is not null)
        {
            if (string.IsNullOrWhiteSpace(issuesFile))
            {
                errorMessages.Add("--issues-file is required");
            }
            else
            {
                options.IssuesFile = issuesFile;
            }
        }

        var generatedTasksFile = result.GetValueForOption(generatedTasksFileOption);
        if (generatedTasksFile is not null)
        {
            if (string.IsNullOrWhiteSpace(generatedTasksFile))
            {
                errorMessages.Add("--generated-tasks-file is required");
            }
            else
            {
                options.GeneratedTasksFile = generatedTasksFile;
            }
        }

        if (result.GetValueForOption(refreshIssuesOption))
        {
            options.RefreshIssues = true;
        }

        var repo = result.GetValueForOption(repoOption);
        if (repo is not null)
        {
            if (string.IsNullOrWhiteSpace(repo))
            {
                errorMessages.Add("--repo is required");
            }
            else
            {
                options.Repo = repo;
            }
        }

        if (result.GetValueForOption(refreshIssuesAzdoOption))
        {
            options.RefreshIssuesAzdo = true;
        }

        var azdoOrganization = result.GetValueForOption(azdoOrganizationOption);
        if (azdoOrganization is not null)
        {
            options.AzdoOrganization = azdoOrganization;
        }

        var azdoProject = result.GetValueForOption(azdoProjectOption);
        if (azdoProject is not null)
        {
            options.AzdoProject = azdoProject;
        }

        var showReasoning = result.GetValueForOption(showReasoningOption);
        if (showReasoning.HasValue)
        {
            options.ShowReasoning = showReasoning.Value;
        }

        var colorizedOutput = result.GetValueForOption(colorizedOutputOption);
        if (colorizedOutput.HasValue)
        {
            options.ColorizedOutput = colorizedOutput.Value;
        }

        var streamEvents = result.GetValueForOption(streamEventsOption);
        if (streamEvents.HasValue)
        {
            options.StreamEvents = streamEvents.Value;
        }

        var cliPath = result.GetValueForOption(cliPathOption);
        if (cliPath is not null)
        {
            if (string.IsNullOrWhiteSpace(cliPath))
            {
                errorMessages.Add("--cli-path is required");
            }
            else
            {
                options.CliPath = cliPath;
            }
        }

        var cliUrl = result.GetValueForOption(cliUrlOption);
        if (cliUrl is not null)
        {
            if (string.IsNullOrWhiteSpace(cliUrl))
            {
                errorMessages.Add("--cli-url is required");
            }
            else
            {
                options.CliUrl = cliUrl;
            }
        }

        var copilotConfigPath = result.GetValueForOption(copilotConfigPathOption);
        if (copilotConfigPath is not null)
        {
            if (string.IsNullOrWhiteSpace(copilotConfigPath))
            {
                errorMessages.Add("--copilot-config-path is required");
            }
            else
            {
                options.CopilotConfigPath = copilotConfigPath;
            }
        }

        var copilotToken = result.GetValueForOption(copilotTokenOption);
        if (copilotToken is not null)
        {
            if (string.IsNullOrWhiteSpace(copilotToken))
            {
                errorMessages.Add("--copilot-token is required");
            }
            else
            {
                options.CopilotToken = copilotToken;
            }
        }

        var toolAllow = result.GetValueForOption(toolAllowOption);
        if (toolAllow is { Length: > 0 })
        {
            var normalized = NormalizeMultiValueOption(toolAllow);
            if (normalized.Length > 0)
            {
                options.ToolAllow = normalized;
            }
        }

        var toolDeny = result.GetValueForOption(toolDenyOption);
        if (toolDeny is { Length: > 0 })
        {
            var normalized = NormalizeMultiValueOption(toolDeny);
            if (normalized.Length > 0)
            {
                options.ToolDeny = normalized;
            }
        }

        var dockerSandbox = result.GetValueForOption(dockerSandboxOption);
        if (dockerSandbox.HasValue)
        {
            options.DockerSandbox = dockerSandbox.Value;
        }

        var dockerImage = result.GetValueForOption(dockerImageOption);
        if (dockerImage is not null)
        {
            if (string.IsNullOrWhiteSpace(dockerImage))
            {
                errorMessages.Add("--docker-image is required");
            }
            else
            {
                options.DockerImage = dockerImage;
            }
        }

        if (result.GetValueForOption(listModelsOption))
        {
            options.ListModels = true;
        }

        if (result.GetValueForOption(listModelsJsonOption))
        {
            options.ListModelsJson = true;
            options.ListModels = true;
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
        var root = new RootCommand("Coralph - Ralph loop runner using GitHub Copilot SDK");
        root.AddOption(new Option<bool>(new[] { "-h", "--help" }, "Show help"));
        root.AddOption(new Option<bool>(new[] { "-v", "--version" }, "Show version"));
        root.AddOption(new Option<int?>("--max-iterations", "Max loop iterations (default: 10)"));
        root.AddOption(new Option<string?>("--model", "Model (default: GPT-5.1-Codex)"));
        root.AddOption(new Option<string?>("--prompt-file", "Prompt file (default: prompt.md)"));
        root.AddOption(new Option<string?>("--progress-file", "Progress file (default: progress.txt)"));
        root.AddOption(new Option<string?>("--issues-file", "Issues json file (default: issues.json)"));
        root.AddOption(new Option<string?>("--generated-tasks-file", "Generated tasks backlog file (default: generated_tasks.json)"));
        root.AddOption(new Option<bool>("--refresh-issues", "Refresh issues.json via `gh issue list`"));
        root.AddOption(new Option<string?>("--repo", "Optional repo override for gh"));
        root.AddOption(new Option<bool>("--refresh-issues-azdo", "Refresh issues.json from Azure Boards via `az boards`"));
        root.AddOption(new Option<string?>("--azdo-organization", "Azure DevOps organization URL (uses az devops defaults if not set)"));
        root.AddOption(new Option<string?>("--azdo-project", "Azure DevOps project name (uses az devops defaults if not set)"));
        root.AddOption(new Option<string?>("--cli-path", "Optional: Copilot CLI executable path"));
        root.AddOption(new Option<string?>("--cli-url", "Optional: connect to existing CLI server"));
        root.AddOption(new Option<string?>("--copilot-config-path", "Optional: Copilot CLI config directory to mount into Docker sandbox"));
        root.AddOption(new Option<string?>("--copilot-token", "Optional: GitHub token for non-interactive Copilot CLI auth (sets GH_TOKEN)"));
        var toolAllowOption = new Option<string[]>("--tool-allow", "Allow tool/permission kinds (repeatable or comma-separated)")
        {
            AllowMultipleArgumentsPerToken = true
        };
        var toolDenyOption = new Option<string[]>("--tool-deny", "Deny tool/permission kinds (repeatable or comma-separated)")
        {
            AllowMultipleArgumentsPerToken = true
        };
        root.AddOption(toolAllowOption);
        root.AddOption(toolDenyOption);
        root.AddOption(new Option<string?>("--config", "Optional: JSON config file (default: coralph.config.json)"));
        root.AddOption(new Option<bool>("--init", "Initialize the repository (issues.json, config, prompt, progress) and exits"));
        root.AddOption(new Option<bool?>("--show-reasoning", "Show reasoning output (default: true)"));
        root.AddOption(new Option<bool?>("--colorized-output", "Use colored output (default: true)"));
        root.AddOption(new Option<bool?>(new[] { "--stream-events", "--event-stream" }, "Emit structured JSON events to stdout"));
        root.AddOption(new Option<bool?>("--docker-sandbox", "Run each iteration inside a Docker container (default: false)"));
        root.AddOption(new Option<string?>("--docker-image", "Docker image for sandbox (default: mcr.microsoft.com/devcontainers/dotnet:10.0)"));
        root.AddOption(new Option<bool>("--list-models", "List available Copilot models and exit"));
        root.AddOption(new Option<bool>("--list-models-json", "List available Copilot models as JSON and exit"));
        return root;
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
}
