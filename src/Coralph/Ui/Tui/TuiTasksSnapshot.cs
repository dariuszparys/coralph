using System.Text.Json;

namespace Coralph.Ui.Tui;

internal sealed record GeneratedTaskSnapshotItem(
    string Id,
    int IssueNumber,
    string Title,
    string Description,
    string Status,
    int Order,
    int Sequence);

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
            Tasks: Array.Empty<GeneratedTaskSnapshotItem>(),
            ReadAtUtc: DateTimeOffset.UtcNow);
    }

    internal int ActiveTaskIndex()
    {
        if (Tasks.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < Tasks.Count; i++)
        {
            if (string.Equals(Tasks[i].Status, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (var i = 0; i < Tasks.Count; i++)
        {
            if (string.Equals(Tasks[i].Status, "open", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }
}

internal sealed class GeneratedTasksSnapshotReader
{
    internal GeneratedTasksSnapshot Read(string path)
    {
        var effectivePath = string.IsNullOrWhiteSpace(path)
            ? TaskBacklog.DefaultBacklogFile
            : path;
        var readAtUtc = DateTimeOffset.UtcNow;

        if (!File.Exists(effectivePath))
        {
            return GeneratedTasksSnapshot.Missing(effectivePath) with { ReadAtUtc = readAtUtc };
        }

        try
        {
            var json = File.ReadAllText(effectivePath);
            using var doc = JsonDocument.Parse(json);
            var tasks = ParseTasks(doc.RootElement);

            return new GeneratedTasksSnapshot(
                Path: effectivePath,
                Exists: true,
                Error: null,
                Tasks: tasks,
                ReadAtUtc: readAtUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new GeneratedTasksSnapshot(
                Path: effectivePath,
                Exists: true,
                Error: ex.Message,
                Tasks: Array.Empty<GeneratedTaskSnapshotItem>(),
                ReadAtUtc: readAtUtc);
        }
    }

    private static IReadOnlyList<GeneratedTaskSnapshotItem> ParseTasks(JsonElement root)
    {
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
            throw new JsonException("generated_tasks.json has an unexpected format");
        }

        var tasks = new List<GeneratedTaskSnapshotItem>();
        var index = 0;

        foreach (var task in tasksElement.EnumerateArray())
        {
            index++;

            if (task.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var issueNumber = ReadInt(task, "issueNumber");
            var order = ReadInt(task, "order");
            var id = ReadString(task, "id");
            var title = ReadString(task, "title");

            tasks.Add(new GeneratedTaskSnapshotItem(
                Id: string.IsNullOrWhiteSpace(id) ? $"task-{index:D3}" : id,
                IssueNumber: issueNumber,
                Title: title,
                Description: ReadString(task, "description"),
                Status: NormalizeStatus(ReadString(task, "status")),
                Order: order <= 0 ? index : order,
                Sequence: index));
        }

        return tasks;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "open";
        }

        var normalized = status.Trim().ToLowerInvariant()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        return normalized switch
        {
            "done" or "completed" or "complete" => "done",
            "in_progress" or "inprogress" => "in_progress",
            "blocked" => "blocked",
            _ => "open",
        };
    }
}
