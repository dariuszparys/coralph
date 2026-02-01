using System.Text;
using System.Text.Json;

namespace Coralph;

/// <summary>
/// Testable utility functions extracted from Program.cs
/// </summary>
internal static class PromptHelpers
{
    internal static string BuildCombinedPrompt(string promptTemplate, string issuesJson, string progress, bool prMode = false, Dictionary<int, PrFeedbackData>? prFeedback = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are running inside a loop. Use the files and repository as your source of truth.");
        sb.AppendLine("Ignore any pre-existing uncommitted changes in the working tree - focus only on the issues listed below.");
        sb.AppendLine();

        // Add PR mode indicator
        if (prMode)
        {
            sb.AppendLine("# WORKFLOW MODE: PULL REQUEST");
            sb.AppendLine("You are operating in PR mode. Do NOT push directly to main or close issues directly.");
            sb.AppendLine("Follow the PR workflow instructions in the INSTRUCTIONS section below.");
            sb.AppendLine();
        }

        sb.AppendLine("# ISSUES_JSON");
        sb.AppendLine("```json");
        sb.AppendLine(issuesJson.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        // Add PR feedback if present
        if (prFeedback is not null && prFeedback.Count > 0)
        {
            sb.AppendLine("# PR_FEEDBACK");
            sb.AppendLine("The following issues have open PRs with review feedback that needs to be addressed:");
            sb.AppendLine("```json");
            sb.AppendLine(JsonSerializer.Serialize(prFeedback.Values, new JsonSerializerOptions { WriteIndented = true }));
            sb.AppendLine("```");
            sb.AppendLine();
        }

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

    internal static void ApplyOverrides(LoopOptions target, LoopOptionsOverrides overrides)
    {
        if (overrides.MaxIterations is { } max) target.MaxIterations = max;
        if (!string.IsNullOrWhiteSpace(overrides.Model)) target.Model = overrides.Model;
        if (!string.IsNullOrWhiteSpace(overrides.PromptFile)) target.PromptFile = overrides.PromptFile;
        if (!string.IsNullOrWhiteSpace(overrides.ProgressFile)) target.ProgressFile = overrides.ProgressFile;
        if (!string.IsNullOrWhiteSpace(overrides.IssuesFile)) target.IssuesFile = overrides.IssuesFile;
        if (overrides.RefreshIssues is { } refresh) target.RefreshIssues = refresh;
        if (!string.IsNullOrWhiteSpace(overrides.Repo)) target.Repo = overrides.Repo;
        if (!string.IsNullOrWhiteSpace(overrides.CliPath)) target.CliPath = overrides.CliPath;
        if (!string.IsNullOrWhiteSpace(overrides.CliUrl)) target.CliUrl = overrides.CliUrl;
        if (!string.IsNullOrWhiteSpace(overrides.CopilotConfigPath)) target.CopilotConfigPath = overrides.CopilotConfigPath;
        if (overrides.ShowReasoning is { } showReasoning) target.ShowReasoning = showReasoning;
        if (overrides.ColorizedOutput is { } colorizedOutput) target.ColorizedOutput = colorizedOutput;
        if (overrides.StreamEvents is { } streamEvents) target.StreamEvents = streamEvents;
        if (overrides.PrMode is { } prMode) target.PrMode = prMode;
        if (overrides.PrModeBypassUsers is { } bypassUsers) target.PrModeBypassUsers = new List<string>(bypassUsers);
        if (overrides.DockerSandbox is { } dockerSandbox) target.DockerSandbox = dockerSandbox;
        if (!string.IsNullOrWhiteSpace(overrides.DockerImage)) target.DockerImage = overrides.DockerImage;
    }

    private static string TrimMarkdownWrapper(string value)
    {
        return value.Trim('`', '*', '_');
    }
}
