using System.Text;

using Coralph;

var (opt, err) = ArgParser.Parse(args);
if (opt is null)
{
    if (err is not null)
    {
        Console.Error.WriteLine(err);
        Console.Error.WriteLine();
    }

    ArgParser.PrintUsage(err is null ? Console.Out : Console.Error);
    return err is null ? 0 : 2;
}

var ct = CancellationToken.None;

if (opt.RefreshIssues)
{
    Console.WriteLine("Refreshing issues...");
    var issuesJson = await GhIssues.FetchOpenIssuesJsonAsync(opt.Repo, ct);
    await File.WriteAllTextAsync(opt.IssuesFile, issuesJson, ct);
}

var promptTemplate = await File.ReadAllTextAsync(opt.PromptFile, ct);
var issues = File.Exists(opt.IssuesFile)
    ? await File.ReadAllTextAsync(opt.IssuesFile, ct)
    : "[]";
var progress = File.Exists(opt.ProgressFile)
    ? await File.ReadAllTextAsync(opt.ProgressFile, ct)
    : string.Empty;

for (var i = 1; i <= opt.MaxIterations; i++)
{
    Console.WriteLine($"\n=== Iteration {i}/{opt.MaxIterations} ===\n");

    var combinedPrompt = BuildCombinedPrompt(promptTemplate, issues, progress);

    string output;
    try
    {
        output = await CopilotRunner.RunOnceAsync(opt, combinedPrompt, ct);
    }
    catch (Exception ex)
    {
        output = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        Console.Error.WriteLine(output);
    }

    var entry = $"\n\n---\n# Iteration {i} ({DateTimeOffset.UtcNow:O})\n\n{output}\n";
    await File.AppendAllTextAsync(opt.ProgressFile, entry, ct);

    progress += entry;

    if (ContainsComplete(output))
    {
        Console.WriteLine("\nCOMPLETE detected, stopping.\n");
        break;
    }
}

return 0;

static string BuildCombinedPrompt(string promptTemplate, string issuesJson, string progress)
{
    var sb = new StringBuilder();

    sb.AppendLine("You are running inside a loop. Use the files and repository as your source of truth.");
    sb.AppendLine("Stop condition: when everything is done, output EXACTLY: <promise>COMPLETE</promise>.");
    sb.AppendLine();

    sb.AppendLine("# ISSUES_JSON");
    sb.AppendLine("```json");
    sb.AppendLine(issuesJson.Trim());
    sb.AppendLine("```");
    sb.AppendLine();

    sb.AppendLine("# PROGRESS_SO_FAR");
    sb.AppendLine("```text");
    sb.AppendLine(string.IsNullOrWhiteSpace(progress) ? "(empty)" : progress.Trim());
    sb.AppendLine("```");
    sb.AppendLine();

    sb.AppendLine("# INSTRUCTIONS");
    sb.AppendLine(promptTemplate.Trim());
    sb.AppendLine();

    sb.AppendLine("# OUTPUT_RULES");
    sb.AppendLine("- If you are done, output EXACTLY: <promise>COMPLETE</promise>");
    sb.AppendLine("- Otherwise, output what you changed and what you will do next iteration.");

    return sb.ToString();
}

static bool ContainsComplete(string output)
{
    if (output.Contains("<promise>COMPLETE</promise>", StringComparison.OrdinalIgnoreCase))
        return true;

    // Back-compat with older sentinel
    return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(l => string.Equals(l, "COMPLETE", StringComparison.OrdinalIgnoreCase));
}
