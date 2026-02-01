namespace Coralph;

internal enum PrMode
{
    Auto,
    Always,
    Never
}

internal sealed class LoopOptions
{
    internal const string ConfigurationSectionName = "LoopOptions";
    internal const string ConfigurationFileName = "coralph.config.json";

    public int MaxIterations { get; set; } = 10;
    public string Model { get; set; } = "GPT-5.1-Codex";

    public string PromptFile { get; set; } = "prompt.md";
    public string ProgressFile { get; set; } = "progress.txt";
    public string IssuesFile { get; set; } = "issues.json";

    public bool RefreshIssues { get; set; }
    public string? Repo { get; set; }

    public string? CliPath { get; set; }
    public string? CliUrl { get; set; }

    public bool ShowReasoning { get; set; } = true;
    public bool ColorizedOutput { get; set; } = true;
    public bool StreamEvents { get; set; }
    public PrMode PrMode { get; set; } = PrMode.Auto;
    public List<string> PrModeBypassUsers { get; set; } = new();
}

internal sealed class LoopOptionsOverrides
{
    public int? MaxIterations { get; set; }
    public string? Model { get; set; }

    public string? PromptFile { get; set; }
    public string? ProgressFile { get; set; }
    public string? IssuesFile { get; set; }

    public bool? RefreshIssues { get; set; }
    public string? Repo { get; set; }

    public string? CliPath { get; set; }
    public string? CliUrl { get; set; }

    public bool? ShowReasoning { get; set; }
    public bool? ColorizedOutput { get; set; }
    public bool? StreamEvents { get; set; }
    public PrMode? PrMode { get; set; }
    public List<string>? PrModeBypassUsers { get; set; }
}
