namespace Coralph.Ui.Tui;

internal enum TranscriptEntryKind
{
    System,
    Assistant,
    Reasoning,
    Tool,
    Error,
    Warning
}

internal sealed record TranscriptEntry(TranscriptEntryKind Kind, string Text, DateTimeOffset Timestamp);

internal sealed class TuiState
{
    private const int MaxTranscriptEntries = 600;

    private readonly object _lock = new();
    private readonly List<TranscriptEntry> _transcript = [];

    private GeneratedTasksSnapshot _tasksSnapshot = GeneratedTasksSnapshot.Missing(TaskBacklog.DefaultBacklogFile);
    private int _taskSelectedIndex = -1;
    private int _transcriptSelectedIndex = -1;
    private bool _transcriptFollow = true;

    private PromptSelectionRequest? _prompt;
    private ExitPromptRequest? _exitPrompt;

    internal void AppendLine(TranscriptEntryKind kind, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            AddEntry(kind, string.Empty, allowCoalesce: false);
            return;
        }

        AddEntry(kind, text, allowCoalesce: false);
    }

    internal void AppendChunk(TranscriptEntryKind kind, string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }

        AddEntry(kind, chunk, allowCoalesce: true);
    }

    internal IReadOnlyList<string> GetTranscriptLines(int maxLines)
    {
        lock (_lock)
        {
            var lines = new List<string>();
            foreach (var entry in _transcript)
            {
                var prefix = KindPrefix(entry.Kind);
                var linePrefix = $"{entry.Timestamp:HH:mm:ss} [{prefix}] ";
                var chunks = entry.Text.Replace("\r\n", "\n").Split('\n');
                for (var i = 0; i < chunks.Length; i++)
                {
                    var text = chunks[i];
                    lines.Add(i == 0 ? linePrefix + text : new string(' ', linePrefix.Length) + text);
                }
            }

            if (lines.Count <= maxLines)
            {
                return lines;
            }

            return lines.TakeLast(maxLines).ToArray();
        }
    }

    internal int GetTranscriptSelectedIndex(int defaultIndex)
    {
        lock (_lock)
        {
            if (_transcriptFollow)
            {
                return defaultIndex;
            }

            return _transcriptSelectedIndex < 0 ? defaultIndex : _transcriptSelectedIndex;
        }
    }

    internal void SetTranscriptSelectedIndex(int index, int latestIndex)
    {
        lock (_lock)
        {
            _transcriptSelectedIndex = index;
            _transcriptFollow = latestIndex < 0 || index >= latestIndex;
        }
    }

    internal bool IsTranscriptFollowEnabled()
    {
        lock (_lock)
        {
            return _transcriptFollow;
        }
    }

    internal GeneratedTasksSnapshot GetTasksSnapshot()
    {
        lock (_lock)
        {
            return _tasksSnapshot;
        }
    }

    internal void SetTasksSnapshot(GeneratedTasksSnapshot snapshot)
    {
        lock (_lock)
        {
            _tasksSnapshot = snapshot;
            if (snapshot.Tasks.Count == 0)
            {
                _taskSelectedIndex = -1;
                return;
            }

            if (_taskSelectedIndex < 0 || _taskSelectedIndex >= snapshot.Tasks.Count)
            {
                _taskSelectedIndex = snapshot.ActiveTaskIndex();
            }
        }
    }

    internal int GetTaskSelectedIndex(int defaultIndex)
    {
        lock (_lock)
        {
            return _taskSelectedIndex < 0 ? defaultIndex : _taskSelectedIndex;
        }
    }

    internal void SetTaskSelectedIndex(int index)
    {
        lock (_lock)
        {
            _taskSelectedIndex = index;
        }
    }

    internal PromptSelectionRequest? GetPrompt()
    {
        lock (_lock)
        {
            return _prompt;
        }
    }

    internal ExitPromptRequest? GetExitPrompt()
    {
        lock (_lock)
        {
            return _exitPrompt;
        }
    }

    internal Task<int> RequestPromptSelectionAsync(string title, IReadOnlyList<string> options, int defaultIndex)
    {
        lock (_lock)
        {
            if (_prompt is not null)
            {
                return _prompt.Completion.Task;
            }

            var clampedDefault = options.Count == 0
                ? 0
                : Math.Clamp(defaultIndex, 0, options.Count - 1);

            _prompt = new PromptSelectionRequest(title, options, clampedDefault);
            return _prompt.Completion.Task;
        }
    }

    internal void UpdatePromptSelection(int index)
    {
        lock (_lock)
        {
            if (_prompt is null)
            {
                return;
            }

            if (_prompt.Options.Count == 0)
            {
                _prompt.SelectedIndex = 0;
                return;
            }

            _prompt.SelectedIndex = Math.Clamp(index, 0, _prompt.Options.Count - 1);
        }
    }

    internal void CompletePromptSelection(int index)
    {
        lock (_lock)
        {
            if (_prompt is null)
            {
                return;
            }

            var selected = _prompt.Options.Count == 0 ? 0 : Math.Clamp(index, 0, _prompt.Options.Count - 1);
            _prompt.Completion.TrySetResult(selected);
            _prompt = null;
        }
    }

    internal void CancelPrompt()
    {
        lock (_lock)
        {
            _prompt?.Completion.TrySetCanceled();
            _prompt = null;
        }
    }

    internal Task WaitForAnyKeyAsync(string message)
    {
        lock (_lock)
        {
            if (_exitPrompt is not null)
            {
                return _exitPrompt.Completion.Task;
            }

            _exitPrompt = new ExitPromptRequest(message);
            return _exitPrompt.Completion.Task;
        }
    }

    internal void CompleteExitPrompt()
    {
        lock (_lock)
        {
            _exitPrompt?.Completion.TrySetResult();
            _exitPrompt = null;
        }
    }

    internal void CancelExitPrompt()
    {
        lock (_lock)
        {
            _exitPrompt?.Completion.TrySetCanceled();
            _exitPrompt = null;
        }
    }

    private void AddEntry(TranscriptEntryKind kind, string text, bool allowCoalesce)
    {
        lock (_lock)
        {
            if (allowCoalesce && _transcript.Count > 0)
            {
                var last = _transcript[^1];
                if (last.Kind == kind && (DateTimeOffset.UtcNow - last.Timestamp) < TimeSpan.FromSeconds(2))
                {
                    _transcript[^1] = last with
                    {
                        Text = last.Text + text,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    return;
                }
            }

            _transcript.Add(new TranscriptEntry(kind, text, DateTimeOffset.UtcNow));
            if (_transcript.Count > MaxTranscriptEntries)
            {
                var removeCount = _transcript.Count - MaxTranscriptEntries;
                _transcript.RemoveRange(0, removeCount);
            }
        }
    }

    private static string KindPrefix(TranscriptEntryKind kind)
    {
        return kind switch
        {
            TranscriptEntryKind.System => "SYS",
            TranscriptEntryKind.Assistant => "AST",
            TranscriptEntryKind.Reasoning => "RSN",
            TranscriptEntryKind.Tool => "TOOL",
            TranscriptEntryKind.Error => "ERR",
            TranscriptEntryKind.Warning => "WRN",
            _ => "LOG"
        };
    }
}

internal sealed class PromptSelectionRequest
{
    internal PromptSelectionRequest(string title, IReadOnlyList<string> options, int selectedIndex)
    {
        Title = title;
        Options = options;
        SelectedIndex = selectedIndex;
    }

    internal string Title { get; }
    internal IReadOnlyList<string> Options { get; }
    internal int SelectedIndex { get; set; }
    internal TaskCompletionSource<int> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class ExitPromptRequest
{
    internal ExitPromptRequest(string message)
    {
        Message = message;
    }

    internal string Message { get; }
    internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
