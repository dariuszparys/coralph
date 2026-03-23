namespace Coralph;

internal static class TaskStatusHelper
{
    internal static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "open";
        }

        var normalized = status.Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        return normalized switch
        {
            "done" or "complete" or "completed" => "done",
            "in_progress" or "inprogress" => "in_progress",
            "blocked" => "blocked",
            "open" => "open",
            _ => "open"
        };
    }
}
