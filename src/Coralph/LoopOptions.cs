namespace Coralph;

internal sealed class LoopOptions
{
    public int MaxIterations { get; init; } = 10;
    public string Model { get; init; } = "gpt-5";

    public string PromptFile { get; init; } = "prompt.md";
    public string ProgressFile { get; init; } = "progress.txt";
    public string IssuesFile { get; init; } = "issues.json";

    public bool RefreshIssues { get; init; }
    public string? Repo { get; init; }

    public string? CliPath { get; init; }
    public string? CliUrl { get; init; }
}
