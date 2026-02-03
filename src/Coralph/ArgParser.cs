using System.CommandLine;
using System.CommandLine.Help;

namespace Coralph;

internal static class ArgParser
{
    internal static (LoopOptionsOverrides? Overrides, string? Error, bool PrintInitialConfig, string? ConfigFile, bool ShowHelp, bool ShowVersion) Parse(string[] args)
    {
        var options = new LoopOptionsOverrides();
        string? configFile = null;
        var showHelp = false;
        var showVersion = false;
        var printInitialConfig = false;
        var errorMessages = new List<string>();

        var root = new RootCommand("Coralph - Ralph loop runner using GitHub Copilot SDK");
        var helpOption = new Option<bool>(new[] { "-h", "--help" }, "Show help");
        var versionOption = new Option<bool>(new[] { "-v", "--version" }, "Show version");
        var maxIterationsOption = new Option<int?>("--max-iterations", "Max loop iterations (default: 10)");
        var modelOption = new Option<string?>("--model", "Model (default: GPT-5.1-Codex)");
        var promptFileOption = new Option<string?>("--prompt-file", "Prompt file (default: prompt.md)");
        var progressFileOption = new Option<string?>("--progress-file", "Progress file (default: progress.txt)");
        var issuesFileOption = new Option<string?>("--issues-file", "Issues json file (default: issues.json)");
        var refreshIssuesOption = new Option<bool>("--refresh-issues", "Refresh issues.json via `gh issue list`");
        var repoOption = new Option<string?>("--repo", "Optional repo override for gh");
        var refreshIssuesAzdoOption = new Option<bool>("--refresh-issues-azdo", "Refresh issues.json from Azure Boards via `az boards`");
        var azdoOrganizationOption = new Option<string?>("--azdo-organization", "Azure DevOps organization URL (uses az devops defaults if not set)");
        var azdoProjectOption = new Option<string?>("--azdo-project", "Azure DevOps project name (uses az devops defaults if not set)");
        var cliPathOption = new Option<string?>("--cli-path", "Optional: Copilot CLI executable path");
        var cliUrlOption = new Option<string?>("--cli-url", "Optional: connect to existing CLI server");
        var copilotConfigPathOption = new Option<string?>("--copilot-config-path", "Optional: Copilot CLI config directory to mount into Docker sandbox");
        var copilotTokenOption = new Option<string?>("--copilot-token", "Optional: GitHub token for non-interactive Copilot CLI auth (sets GH_TOKEN)");
        var configOption = new Option<string?>("--config", "Optional: JSON config file (default: coralph.config.json)");
        var initialConfigOption = new Option<bool>("--initial-config", "Writes default config json and exits");
        var showReasoningOption = new Option<bool?>("--show-reasoning", "Show reasoning output (default: true)");
        var colorizedOutputOption = new Option<bool?>("--colorized-output", "Use colored output (default: true)");
        var streamEventsOption = new Option<bool?>(new[] { "--stream-events", "--event-stream" }, "Emit structured JSON events to stdout");
        var dockerSandboxOption = new Option<bool?>("--docker-sandbox", "Run each iteration inside a Docker container (default: false)");
        var dockerImageOption = new Option<string?>("--docker-image", "Docker image for sandbox (default: mcr.microsoft.com/devcontainers/dotnet:10.0)");

        root.AddOption(helpOption);
        root.AddOption(versionOption);
        root.AddOption(maxIterationsOption);
        root.AddOption(modelOption);
        root.AddOption(promptFileOption);
        root.AddOption(progressFileOption);
        root.AddOption(issuesFileOption);
        root.AddOption(refreshIssuesOption);
        root.AddOption(repoOption);
        root.AddOption(refreshIssuesAzdoOption);
        root.AddOption(azdoOrganizationOption);
        root.AddOption(azdoProjectOption);
        root.AddOption(cliPathOption);
        root.AddOption(cliUrlOption);
        root.AddOption(copilotConfigPathOption);
        root.AddOption(copilotTokenOption);
        root.AddOption(configOption);
        root.AddOption(initialConfigOption);
        root.AddOption(showReasoningOption);
        root.AddOption(colorizedOutputOption);
        root.AddOption(streamEventsOption);
        root.AddOption(dockerSandboxOption);
        root.AddOption(dockerImageOption);

        var result = root.Parse(args);
        showHelp = result.GetValueForOption(helpOption);
        showVersion = result.GetValueForOption(versionOption);
        printInitialConfig = result.GetValueForOption(initialConfigOption);
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

        if (result.Errors.Count > 0)
        {
            errorMessages.AddRange(result.Errors.Select(e => e.Message));
        }

        if (showHelp)
        {
            return (null, null, printInitialConfig, configFile, true, false);
        }

        if (showVersion)
        {
            return (null, null, printInitialConfig, configFile, false, true);
        }

        if (errorMessages.Count > 0)
        {
            return (null, string.Join(Environment.NewLine, errorMessages), printInitialConfig, configFile, false, false);
        }

        return (options, null, printInitialConfig, configFile, false, false);
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
        root.AddOption(new Option<bool>("--refresh-issues", "Refresh issues.json via `gh issue list`"));
        root.AddOption(new Option<string?>("--repo", "Optional repo override for gh"));
        root.AddOption(new Option<bool>("--refresh-issues-azdo", "Refresh issues.json from Azure Boards via `az boards`"));
        root.AddOption(new Option<string?>("--azdo-organization", "Azure DevOps organization URL (uses az devops defaults if not set)"));
        root.AddOption(new Option<string?>("--azdo-project", "Azure DevOps project name (uses az devops defaults if not set)"));
        root.AddOption(new Option<string?>("--cli-path", "Optional: Copilot CLI executable path"));
        root.AddOption(new Option<string?>("--cli-url", "Optional: connect to existing CLI server"));
        root.AddOption(new Option<string?>("--copilot-config-path", "Optional: Copilot CLI config directory to mount into Docker sandbox"));
        root.AddOption(new Option<string?>("--copilot-token", "Optional: GitHub token for non-interactive Copilot CLI auth (sets GH_TOKEN)"));
        root.AddOption(new Option<string?>("--config", "Optional: JSON config file (default: coralph.config.json)"));
        root.AddOption(new Option<bool>("--initial-config", "Writes default config json and exits"));
        root.AddOption(new Option<bool?>("--show-reasoning", "Show reasoning output (default: true)"));
        root.AddOption(new Option<bool?>("--colorized-output", "Use colored output (default: true)"));
        root.AddOption(new Option<bool?>(new[] { "--stream-events", "--event-stream" }, "Emit structured JSON events to stdout"));
        root.AddOption(new Option<bool?>("--docker-sandbox", "Run each iteration inside a Docker container (default: false)"));
        root.AddOption(new Option<string?>("--docker-image", "Docker image for sandbox (default: mcr.microsoft.com/devcontainers/dotnet:10.0)"));
        return root;
    }
}
