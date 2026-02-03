using System.Diagnostics;
using System.Text.Json;

namespace Coralph;

internal static class AzBoards
{
    internal static async Task<string> FetchOpenWorkItemsJsonAsync(string? organization, string? project, CancellationToken ct)
    {
        // Build WIQL query for open work items (PBIs, Bugs, Tasks)
        var wiql = "SELECT [System.Id], [System.Title], [System.Description], [System.State], [System.WorkItemType], [System.Tags] " +
                   "FROM workitems " +
                   "WHERE [System.State] <> 'Closed' AND [System.State] <> 'Removed' " +
                   "ORDER BY [System.ChangedDate] DESC";

        // Build az boards query command
        var args = $"boards query --wiql \"{wiql}\" --fields System.Id System.Title System.Description System.State System.WorkItemType System.Tags --output json";
        if (!string.IsNullOrWhiteSpace(organization))
        {
            args += $" --organization {organization}";
        }
        if (!string.IsNullOrWhiteSpace(project))
        {
            args += $" --project {project}";
        }

        var psi = new ProcessStartInfo("az", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start `az`");
        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);

        await p.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"`az boards query` failed (exit {p.ExitCode}): {stderr}");
        }

        // Parse the output and transform to GitHub-compatible format
        return TransformWorkItemsToIssuesFormat(stdout);
    }

    private static string TransformWorkItemsToIssuesFormat(string azBoardsJson)
    {
        // az boards query returns an array of work items
        // We need to transform them to match GitHub issues format:
        // [{ "number": int, "title": string, "body": string, "url": string, "labels": [], "comments": [] }]

        using var doc = JsonDocument.Parse(azBoardsJson);
        var workItems = doc.RootElement;

        var issues = new List<object>();

        foreach (var item in workItems.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // Extract work item fields
            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            var fields = item.TryGetProperty("fields", out var fieldsProp) ? fieldsProp : default;

            if (fields.ValueKind != JsonValueKind.Object || id == 0)
            {
                continue;
            }

            var title = fields.TryGetProperty("System.Title", out var titleProp) ? titleProp.GetString() : null;
            var description = fields.TryGetProperty("System.Description", out var descProp) ? descProp.GetString() : null;
            var state = fields.TryGetProperty("System.State", out var stateProp) ? stateProp.GetString() : null;
            var workItemType = fields.TryGetProperty("System.WorkItemType", out var typeProp) ? typeProp.GetString() : null;
            var tags = fields.TryGetProperty("System.Tags", out var tagsProp) ? tagsProp.GetString() : null;

            // Build URL (requires organization and project from az devops defaults or config)
            var url = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : $"Work Item {id}";

            // Transform tags into labels
            var labels = new List<object>();
            if (!string.IsNullOrWhiteSpace(tags))
            {
                foreach (var tag in tags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    labels.Add(new
                    {
                        name = tag,
                        color = "0366d6", // Default blue color
                        description = ""
                    });
                }
            }

            // Add work item type as a label
            if (!string.IsNullOrWhiteSpace(workItemType))
            {
                labels.Add(new
                {
                    name = workItemType,
                    color = "d73a4a", // Red color for type
                    description = "Work item type"
                });
            }

            // Build body with state and description
            var body = $"**State:** {state}\n\n";
            if (!string.IsNullOrWhiteSpace(description))
            {
                // Strip HTML tags from description (Azure Boards uses HTML)
                var plainText = StripHtml(description);
                body += plainText;
            }

            issues.Add(new
            {
                number = id,
                title = title ?? $"Work Item {id}",
                body,
                url = url ?? $"Work Item {id}",
                labels,
                comments = Array.Empty<object>() // Azure Boards comments would require separate API calls
            });
        }

        return JsonSerializer.Serialize(issues, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        // Simple HTML stripping - replace common tags with appropriate text
        var text = html
            .Replace("<br>", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br />", "\n")
            .Replace("</p>", "\n\n")
            .Replace("</div>", "\n");

        // Remove all remaining HTML tags
        var result = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", string.Empty);

        // Decode HTML entities
        result = System.Net.WebUtility.HtmlDecode(result);

        return result.Trim();
    }
}
