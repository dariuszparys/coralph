using System.Globalization;

namespace Coralph;

internal static class ArgParser
{
    internal static (LoopOptions? Options, string? Error) Parse(string[] args)
    {
        var maxIterations = 10;
        string model = "gpt-5.1-codex";

        string promptFile = "prompt.md";
        string progressFile = "progress.txt";
        string issuesFile = "issues.json";

        bool refreshIssues = false;
        string? repo = null;

        string? cliPath = null;
        string? cliUrl = null;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h":
                case "--help":
                    return (null, null);

                case "--max-iterations":
                    if (!TryGetValue(args, ref i, out var it) ||
                        !int.TryParse(it, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxIterations) ||
                        maxIterations < 1)
                    {
                        return (null, "--max-iterations must be an integer >= 1");
                    }
                    break;

                case "--model":
                    if (!TryGetValue(args, ref i, out model) || string.IsNullOrWhiteSpace(model))
                        return (null, "--model is required");
                    break;

                case "--prompt-file":
                    if (!TryGetValue(args, ref i, out promptFile) || string.IsNullOrWhiteSpace(promptFile))
                        return (null, "--prompt-file is required");
                    break;

                case "--progress-file":
                    if (!TryGetValue(args, ref i, out progressFile) || string.IsNullOrWhiteSpace(progressFile))
                        return (null, "--progress-file is required");
                    break;

                case "--issues-file":
                    if (!TryGetValue(args, ref i, out issuesFile) || string.IsNullOrWhiteSpace(issuesFile))
                        return (null, "--issues-file is required");
                    break;

                case "--refresh-issues":
                    refreshIssues = true;
                    break;

                case "--repo":
                    if (!TryGetValue(args, ref i, out var r) || string.IsNullOrWhiteSpace(r))
                        return (null, "--repo is required");
                    repo = r;
                    break;

                case "--cli-path":
                    if (!TryGetValue(args, ref i, out var cp) || string.IsNullOrWhiteSpace(cp))
                        return (null, "--cli-path is required");
                    cliPath = cp;
                    break;

                case "--cli-url":
                    if (!TryGetValue(args, ref i, out var cu) || string.IsNullOrWhiteSpace(cu))
                        return (null, "--cli-url is required");
                    cliUrl = cu;
                    break;

                default:
                    return (null, $"Unknown argument: {a}");
            }
        }

        return (new LoopOptions
        {
            MaxIterations = maxIterations,
            Model = model,
            PromptFile = promptFile,
            ProgressFile = progressFile,
            IssuesFile = issuesFile,
            RefreshIssues = refreshIssues,
            Repo = repo,
            CliPath = cliPath,
            CliUrl = cliUrl,
        }, null);
    }

    private static bool TryGetValue(string[] args, ref int i, out string value)
    {
        if (i + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }
        i++;
        value = args[i];
        return true;
    }

    internal static void PrintUsage(TextWriter w)
    {
        w.WriteLine("Coralph - Ralph loop runner using GitHub Copilot SDK");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  dotnet run --project src/Coralph -- [options]");
        w.WriteLine();
        w.WriteLine("Options:");
        w.WriteLine("  --max-iterations <n>   Max loop iterations (default: 10)");
        w.WriteLine("  --model <name>         Model (default: gpt-5.1-codex)");
        w.WriteLine("  --prompt-file <path>   Prompt file (default: prompt.md)");
        w.WriteLine("  --progress-file <path> Progress file (default: progress.txt)");
        w.WriteLine("  --issues-file <path>   Issues json file (default: issues.json)");
        w.WriteLine("  --refresh-issues       Refresh issues.json via `gh issue list`");
        w.WriteLine("  --repo <owner/name>    Optional repo override for gh");
        w.WriteLine("  --cli-path <path>      Optional: Copilot CLI executable path");
        w.WriteLine("  --cli-url <host:port>  Optional: connect to existing CLI server");
        w.WriteLine();
        w.WriteLine("The loop stops early when the assistant output contains the sentinel: COMPLETE");
    }
}
