namespace Coralph;

internal sealed class LoopOptions
{
    internal const string ConfigurationSectionName = "LoopOptions";
    internal const string ConfigurationFileName = "coralph.config.json";

    public int MaxIterations { get; set; } = 10;
    public string Model { get; set; } = "GPT-5.1-Codex";

    public string PromptFile { get; set; } = "prompt.md";
    public string ProgressFile { get; set; } = "progress.txt";
    public string IssuesFile { get; set; } = "issues.json";
    public string? PrdFile { get; set; }

    public bool Banner { get; set; }
    public bool RefreshIssues { get; set; }
    public string? Repo { get; set; }

    public bool GenerateIssues { get; set; }
    public string? CliPath { get; set; }
    public string? CliUrl { get; set; }
}

internal sealed class LoopOptionsOverrides
{
    public int? MaxIterations { get; set; }
    public string? Model { get; set; }

    public string? PromptFile { get; set; }
    public string? ProgressFile { get; set; }
    public string? IssuesFile { get; set; }
    public string? PrdFile { get; set; }

    public bool? Banner { get; set; }
    public bool? RefreshIssues { get; set; }
    public string? Repo { get; set; }

    public bool? GenerateIssues { get; set; }
    public string? CliPath { get; set; }
    public string? CliUrl { get; set; }
}
