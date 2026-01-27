using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Coralph;

internal static class CustomTools
{
    internal static AIFunction[] GetDefaultTools(string issuesFile, string progressFile)
    {
        return
        [
            AIFunctionFactory.Create(
                ([Description("Include closed issues in results")] bool? includeClosed) =>
                    ListOpenIssuesAsync(issuesFile, includeClosed ?? false),
                "list_open_issues",
                "List all open issues from issues.json with their number, title, body, and state"
            ),
            AIFunctionFactory.Create(
                ([Description("Number of recent entries to return")] int? count) =>
                    GetProgressSummaryAsync(progressFile, count ?? 5),
                "get_progress_summary",
                "Get recent progress entries from progress.txt"
            ),
            AIFunctionFactory.Create(
                ([Description("Search term to find in progress")] string searchTerm) =>
                    SearchProgressAsync(progressFile, searchTerm),
                "search_progress",
                "Search progress.txt for specific terms or phrases"
            ),
        ];
    }

    internal static async Task<object> ListOpenIssuesAsync(string issuesFile, bool includeClosed)
    {
        if (!File.Exists(issuesFile))
        {
            return new { error = "issues.json not found", issues = Array.Empty<object>() };
        }

        var json = await File.ReadAllTextAsync(issuesFile);
        using var doc = JsonDocument.Parse(json);
        var issues = new List<object>();

        foreach (var issue in doc.RootElement.EnumerateArray())
        {
            if (issue.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var state = issue.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String
                ? stateProp.GetString()
                : "open";

            if (!includeClosed && string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var number = issue.TryGetProperty("number", out var numberProp) ? numberProp.GetInt32() : 0;
            var title = issue.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                ? titleProp.GetString()
                : string.Empty;
            var body = issue.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String
                ? bodyProp.GetString()
                : string.Empty;

            issues.Add(new { number, title, body, state });
        }

        return new { count = issues.Count, issues };
    }

    internal static async Task<object> GetProgressSummaryAsync(string progressFile, int count)
    {
        if (!File.Exists(progressFile))
        {
            return new { error = "progress.txt not found", entries = Array.Empty<string>() };
        }

        var content = await File.ReadAllTextAsync(progressFile);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new { message = "progress.txt is empty", entries = Array.Empty<string>() };
        }

        var entries = content.Split(new[] { "\n---\n", "\r\n---\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(count)
            .ToArray();

        return new { count = entries.Length, entries };
    }

    internal static async Task<object> SearchProgressAsync(string progressFile, string searchTerm)
    {
        if (!File.Exists(progressFile))
        {
            return new { error = "progress.txt not found", matches = Array.Empty<string>() };
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new { error = "searchTerm cannot be empty", matches = Array.Empty<string>() };
        }

        var content = await File.ReadAllTextAsync(progressFile);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var matches = lines.Where(line => line.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new { searchTerm, matchCount = matches.Length, matches };
    }
}

