namespace Coralph;

internal sealed class LoopOptions
{
    internal const string ConfigurationSectionName = "LoopOptions";
    internal const string ConfigurationFileName = "coralph.config.json";

    public int MaxIterations { get; set; } = 10;
    public string Model { get; set; } = "GPT-5.1-Codex";
    public string? ProviderType { get; set; }
    public string? ProviderBaseUrl { get; set; }
    public string? ProviderWireApi { get; set; }
    public string? ProviderApiKey { get; set; }

    public string PromptFile { get; set; } = "prompt.md";
    public string ProgressFile { get; set; } = "progress.txt";
    public string IssuesFile { get; set; } = "issues.json";
    public string GeneratedTasksFile { get; set; } = TaskBacklog.DefaultBacklogFile;

    public bool RefreshIssues { get; set; }
    public string? Repo { get; set; }

    public bool RefreshIssuesAzdo { get; set; }
    public string? AzdoOrganization { get; set; }
    public string? AzdoProject { get; set; }

    public string? CliPath { get; set; }
    public string? CliUrl { get; set; }
    public string? CopilotConfigPath { get; set; }
    public string? CopilotToken { get; set; }
    public string[] ToolAllow { get; set; } = [];
    public string[] ToolDeny { get; set; } = [];

    public bool ShowReasoning { get; set; } = true;
    public bool ColorizedOutput { get; set; } = true;
    public bool StreamEvents { get; set; }
    public bool DockerSandbox { get; set; }
    public string DockerImage { get; set; } = "mcr.microsoft.com/devcontainers/dotnet:10.0";
    public bool ListModels { get; set; }
    public bool ListModelsJson { get; set; }
}

internal sealed class LoopOptionsOverrides
{
    public int? MaxIterations { get; set; }
    public string? Model { get; set; }
    public string? ProviderType { get; set; }
    public string? ProviderBaseUrl { get; set; }
    public string? ProviderWireApi { get; set; }
    public string? ProviderApiKey { get; set; }

    public string? PromptFile { get; set; }
    public string? ProgressFile { get; set; }
    public string? IssuesFile { get; set; }
    public string? GeneratedTasksFile { get; set; }

    public bool? RefreshIssues { get; set; }
    public string? Repo { get; set; }

    public bool? RefreshIssuesAzdo { get; set; }
    public string? AzdoOrganization { get; set; }
    public string? AzdoProject { get; set; }

    public string? CliPath { get; set; }
    public string? CliUrl { get; set; }
    public string? CopilotConfigPath { get; set; }
    public string? CopilotToken { get; set; }
    public string[]? ToolAllow { get; set; }
    public string[]? ToolDeny { get; set; }

    public bool? ShowReasoning { get; set; }
    public bool? ColorizedOutput { get; set; }
    public bool? StreamEvents { get; set; }
    public bool? DockerSandbox { get; set; }
    public string? DockerImage { get; set; }
    public bool? ListModels { get; set; }
    public bool? ListModelsJson { get; set; }
}
