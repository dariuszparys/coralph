using System.Text.Json;

namespace Coralph.Ui.Tui;

internal sealed record GeneratedTaskSnapshotItem(
    string Id,
    int IssueNumber,
    string Title,
    string Description,
    string Status,
    int Order,
    int SourceIndex);

internal sealed record GeneratedTasksSnapshot(
    string Path,
    bool Exists,
    string? Error,
    IReadOnlyList<GeneratedTaskSnapshotItem> Tasks,
    DateTimeOffset ReadAtUtc)
{
    internal static GeneratedTasksSnapshot Missing(string path)
    {
        return new GeneratedTasksSnapshot(
            Path: path,
            Exists: false,
            Error: "generated_tasks.json not found",
            Tasks: [],
            ReadAtUtc: DateTimeOffset.UtcNow);
    }

    internal int ActiveTaskIndex()
    {
        if (Tasks.Count == 0)
        {
            return 0;
        }

        var inProgress = FindStatusIndex("in_progress");
        if (inProgress >= 0)
        {
            return inProgress;
        }

        var open = FindStatusIndex("open");
        if (open >= 0)
        {
            return open;
        }

        return 0;
    }

    private int FindStatusIndex(string status)
    {
        for (var i = 0; i < Tasks.Count; i++)
        {
            if (string.Equals(Tasks[i].Status, status, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}

internal sealed class GeneratedTasksSnapshotReader
{
    internal GeneratedTasksSnapshot Read(string? path)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(path)
            ? TaskBacklog.DefaultBacklogFile
            : path;

        if (!File.Exists(resolvedPath))
        {
            return GeneratedTasksSnapshot.Missing(resolvedPath);
        }

        try
        {
            using var stream = File.OpenRead(resolvedPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            JsonElement tasksElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                tasksElement = root;
            }
            else if (root.ValueKind == JsonValueKind.Object &&
                     root.TryGetProperty("tasks", out var tasksProperty) &&
                     tasksProperty.ValueKind == JsonValueKind.Array)
            {
                tasksElement = tasksProperty;
            }
            else
            {
                return new GeneratedTasksSnapshot(
                    Path: resolvedPath,
                    Exists: true,
                    Error: "generated_tasks.json has an unexpected format",
                    Tasks: [],
                    ReadAtUtc: DateTimeOffset.UtcNow);
            }

            var tasks = new List<GeneratedTaskSnapshotItem>();
            var sourceIndex = 0;

            foreach (var task in tasksElement.EnumerateArray())
            {
                if (task.ValueKind != JsonValueKind.Object)
                {
                    sourceIndex++;
                    continue;
                }

                var id = ReadString(task, "id");
                var issueNumber = ReadInt(task, "issueNumber");
                var title = ReadString(task, "title");
                var description = ReadString(task, "description");
                var status = NormalizeStatus(ReadString(task, "status"));
                var order = ReadInt(task, "order");

                tasks.Add(new GeneratedTaskSnapshotItem(id, issueNumber, title, description, status, order, sourceIndex));
                sourceIndex++;
            }

            var orderedTasks = tasks
                .OrderBy(t => t.Order)
                .ThenBy(t => t.SourceIndex)
                .ToArray();

            return new GeneratedTasksSnapshot(
                Path: resolvedPath,
                Exists: true,
                Error: null,
                Tasks: orderedTasks,
                ReadAtUtc: DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new GeneratedTasksSnapshot(
                Path: resolvedPath,
                Exists: File.Exists(resolvedPath),
                Error: ex.Message,
                Tasks: [],
                ReadAtUtc: DateTimeOffset.UtcNow);
        }
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : 0;
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "open";
        }

        var normalized = status.Trim().ToLowerInvariant().Replace("-", "_");
        return normalized switch
        {
            "done" or "complete" or "completed" => "done",
            "in_progress" or "inprogress" => "in_progress",
            _ => "open"
        };
    }
}
