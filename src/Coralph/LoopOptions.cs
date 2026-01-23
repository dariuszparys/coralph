namespace Coralph;

internal sealed class LoopOptions
{
    public int MaxIterations { get; set; } = 10;
    public string Model { get; set; } = "GPT-5.1-Codex";

    public string PromptFile { get; set; } = "prompt.md";
    public string ProgressFile { get; set; } = "progress.txt";
    public string IssuesFile { get; set; } = "issues.json";

    public bool RefreshIssues { get; set; }
    public string? Repo { get; set; }

    public string? CliPath { get; set; }
    public string? CliUrl { get; set; }
}
