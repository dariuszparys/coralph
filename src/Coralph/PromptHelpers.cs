using System.Text;
using System.Text.Json;

namespace Coralph;

/// <summary>
/// Testable utility functions extracted from Program.cs
/// </summary>
internal static class PromptHelpers
{
    internal static string BuildCombinedPrompt(string promptTemplate, string issuesJson, string progress, string? generatedTasksJson = null, bool dryRun = false)
    {
        var sb = new StringBuilder();

        if (dryRun)
        {
            sb.AppendLine("# DRY RUN MODE");
            sb.AppendLine("You are running in DRY RUN mode. Do NOT write any files, do NOT run any git commands, and do NOT commit or push changes.");
            sb.AppendLine("Instead, describe exactly what you WOULD do: output \"[DRY RUN] WOULD WRITE: <path>\" for each file you would change,");
            sb.AppendLine("\"[DRY RUN] WOULD COMMIT: <message>\" for each commit you would make, and a final summary of what would change.");
            sb.AppendLine();
        }

        sb.AppendLine("You are running inside a loop. Use the files and repository as your source of truth.");
        sb.AppendLine("Ignore any pre-existing uncommitted changes in the working tree - focus only on the issues listed below.");
        sb.AppendLine();

        sb.AppendLine("# ISSUES_JSON");
        sb.AppendLine("```json");
        sb.AppendLine(issuesJson.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("# GENERATED_TASKS_JSON");
        sb.AppendLine("```json");
        sb.AppendLine(string.IsNullOrWhiteSpace(generatedTasksJson) ? "{\"version\":1,\"sourceIssueCount\":0,\"tasks\":[]}" : generatedTasksJson.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("# PROGRESS_SO_FAR");
        sb.AppendLine("```text");
        sb.AppendLine(string.IsNullOrWhiteSpace(progress) ? "(empty)" : progress.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("# INSTRUCTIONS");
        sb.AppendLine(promptTemplate.Trim());

        return sb.ToString();
    }

    internal static bool TryGetHasOpenIssues(string issuesJson, out bool hasOpenIssues, out string? error)
    {
        hasOpenIssues = false;
        error = null;

        if (string.IsNullOrWhiteSpace(issuesJson))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(issuesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "issues.json must be a JSON array.";
                return false;
            }

            foreach (var issue in doc.RootElement.EnumerateArray())
            {
                if (issue.ValueKind != JsonValueKind.Object)
                {
                    hasOpenIssues = true;
                    break;
                }

                if (issue.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String)
                {
                    var stateValue = state.GetString();
                    if (string.Equals(stateValue, "closed", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                hasOpenIssues = true;
                break;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"Failed to parse issues JSON: {ex.Message}";
            return false;
        }
    }

    internal static bool TryGetTerminalSignal(string output, out string signal)
    {
        signal = string.Empty;

        if (string.IsNullOrWhiteSpace(output))
            return false;

        if (output.Contains("<promise>COMPLETE</promise>", StringComparison.OrdinalIgnoreCase))
        {
            signal = "COMPLETE";
            return true;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            line = TrimMarkdownWrapper(line);

            if (line.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                signal = "COMPLETE";
                return true;
            }

            if (line.Equals("ALL_TASKS_COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                signal = "ALL_TASKS_COMPLETE";
                return true;
            }

            if (line.Equals("NO_OPEN_ISSUES", StringComparison.OrdinalIgnoreCase))
            {
                signal = "NO_OPEN_ISSUES";
                return true;
            }
        }

        return false;
    }

    internal static bool ContainsComplete(string output)
    {
        return TryGetTerminalSignal(output, out var signal) &&
               string.Equals(signal, "COMPLETE", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimMarkdownWrapper(string value)
    {
        return value.Trim('`', '*', '_');
    }
}
