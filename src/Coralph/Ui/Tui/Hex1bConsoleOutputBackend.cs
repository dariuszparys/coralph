using System.Text;
using System.Text.RegularExpressions;
using Coralph.Ui;
using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;
using Spectre.Console;

namespace Coralph.Ui.Tui;

internal sealed class Hex1bConsoleOutputBackend : IConsoleOutputBackend
{
    private static readonly Regex MarkupRegex = new(@"\[[^\]]+\]", RegexOptions.Compiled);

    private readonly TuiState _state = new();
    private readonly GeneratedTasksSnapshotReader _tasksReader = new();
    private readonly LoopOptions _options;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<ConsoleOutputBackendExit> _exitTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Hex1bApp? _app;
    private Task? _uiTask;
    private Task? _pollTask;
    private int _exitSignaled;
    private bool _disposeRequested;
    private DateTime _lastGeneratedTasksWriteTimeUtc = DateTime.MinValue;

    private IAnsiConsole _out;
    private IAnsiConsole _error;

    public Hex1bConsoleOutputBackend(LoopOptions options)
    {
        _options = options;
        _out = ConsoleOutput.CreateConsole(Console.Out, Console.IsOutputRedirected);
        _error = ConsoleOutput.CreateConsole(Console.Error, Console.IsErrorRedirected);
        _state.SetSelectedModel(options.Model);
    }

    public bool UsesTui => true;
    public Task<ConsoleOutputBackendExit>? ExitTask => _exitTcs.Task;

    public IAnsiConsole Out => _out;

    public IAnsiConsole Error => _error;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        if (_uiTask is not null)
        {
            return Task.CompletedTask;
        }

        _state.AppendLine(TranscriptEntryKind.System, $"Coralph {Banner.GetVersion()}");
        _state.AppendLine(TranscriptEntryKind.System, "TUI mode active");

        RefreshGeneratedTasks();

        _uiTask = Task.Run(() => RunUiAsync(_cts.Token), _cts.Token);
        _pollTask = Task.Run(() => PollGeneratedTasksAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _disposeRequested = true;
        _state.CancelPrompt();
        _state.CancelQuitPrompt();
        _state.CancelExitPrompt();

        _cts.Cancel();

        if (_app is not null)
        {
            try
            {
                _app.RequestStop();
            }
            catch
            {
                // Best effort: app might already be stopping.
            }
        }

        if (_pollTask is not null)
        {
            try
            {
                await _pollTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        if (_uiTask is not null)
        {
            try
            {
                await _uiTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        _cts.Dispose();
    }

    public void Configure(IAnsiConsole? stdout, IAnsiConsole? stderr)
    {
        _out = stdout ?? ConsoleOutput.CreateConsole(Console.Out, Console.IsOutputRedirected);
        _error = stderr ?? ConsoleOutput.CreateConsole(Console.Error, Console.IsErrorRedirected);
    }

    public void Reset()
    {
        // No-op: TUI backend maintains state for the whole app lifetime.
    }

    public void Write(string text)
    {
        _state.AppendChunk(TranscriptEntryKind.System, text);
        RequestInvalidate();
    }

    public void WriteLine()
    {
        _state.AppendLine(TranscriptEntryKind.System, string.Empty);
        RequestInvalidate();
    }

    public void WriteLine(string text)
    {
        _state.AppendLine(TranscriptEntryKind.System, text);
        RequestInvalidate();
    }

    public void WriteError(string text)
    {
        _state.AppendChunk(TranscriptEntryKind.Error, text);
        RequestInvalidate();
    }

    public void WriteErrorLine()
    {
        _state.AppendLine(TranscriptEntryKind.Error, string.Empty);
        RequestInvalidate();
    }

    public void WriteErrorLine(string text)
    {
        _state.AppendLine(TranscriptEntryKind.Error, text);
        RequestInvalidate();
    }

    public void WriteWarningLine(string text)
    {
        _state.AppendLine(TranscriptEntryKind.Warning, text);
        RequestInvalidate();
    }

    public void MarkupLine(string markup)
    {
        _state.AppendLine(TranscriptEntryKind.System, StripMarkup(markup));
        RequestInvalidate();
    }

    public void MarkupLineInterpolated(FormattableString markup)
    {
        _state.AppendLine(TranscriptEntryKind.System, StripMarkup(markup.ToString()));
        RequestInvalidate();
    }

    public void WriteReasoning(string text)
    {
        _state.AppendChunk(TranscriptEntryKind.Reasoning, text);
        RequestInvalidate();
    }

    public void WriteAssistant(string text)
    {
        _state.AppendChunk(TranscriptEntryKind.Assistant, text);
        RequestInvalidate();
    }

    public void WriteToolStart(string toolName)
    {
        _state.AppendLine(TranscriptEntryKind.Tool, $"Tool start: {toolName}");
        RequestInvalidate();
    }

    public void WriteToolComplete(string toolName, string summary)
    {
        var message = string.IsNullOrWhiteSpace(summary)
            ? $"Tool done: {toolName}"
            : $"Tool done: {toolName} - {summary}";
        _state.AppendLine(TranscriptEntryKind.Tool, message);
        RequestInvalidate();
    }

    public void WriteSectionSeparator(string title)
    {
        _state.AppendLine(TranscriptEntryKind.System, $"--- {title} ---");
        RequestInvalidate();
    }

    public void SetSelectedModel(string? model)
    {
        _state.SetSelectedModel(model);
        RequestInvalidate();
    }

    public void RefreshGeneratedTasks()
    {
        var path = _options.GeneratedTasksFile;
        var currentWriteTimeUtc = File.Exists(path)
            ? File.GetLastWriteTimeUtc(path)
            : DateTime.MinValue;

        if (currentWriteTimeUtc == _lastGeneratedTasksWriteTimeUtc)
        {
            return;
        }

        _lastGeneratedTasksWriteTimeUtc = currentWriteTimeUtc;

        var snapshot = _tasksReader.Read(path);
        _state.SetTasksSnapshot(snapshot);
        RequestInvalidate();
    }

    public async Task<int> PromptSelectionAsync(
        string title,
        IReadOnlyList<string> options,
        int defaultIndex,
        CancellationToken ct = default)
    {
        if (options.Count == 0)
        {
            return defaultIndex;
        }

        var selectionTask = _state.RequestPromptSelectionAsync(title, options, defaultIndex);
        RequestInvalidate();

        try
        {
            return await selectionTask.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _state.CancelPrompt();
            throw;
        }
        finally
        {
            RequestInvalidate();
        }
    }

    internal async Task WaitForAnyKeyAsync(string message, CancellationToken ct = default)
    {
        _state.AppendLine(TranscriptEntryKind.System, message);
        var waitTask = _state.WaitForAnyKeyAsync(message);
        RequestInvalidate();

        try
        {
            await waitTask.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _state.CancelExitPrompt();
            throw;
        }
        catch (OperationCanceledException)
        {
            // The TUI backend can be disposed when switching to classic output.
            // Treat that as a normal end to the TUI-specific wait.
        }
        finally
        {
            RequestInvalidate();
        }
    }

    private async Task RunUiAsync(CancellationToken ct)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, _) =>
            {
                _app = app;
                return BuildRootWidget;
            })
            .WithMouse()
            .Build();

        try
        {
            await terminal.RunAsync(ct).ConfigureAwait(false);
            if (!_disposeRequested)
            {
                SignalExit(ConsoleOutputBackendExitReason.UnexpectedFailure, "TUI stopped unexpectedly");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            SignalExit(ConsoleOutputBackendExitReason.UnexpectedFailure, ex.Message, ex);
        }
    }

    private async Task PollGeneratedTasksAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var path = _options.GeneratedTasksFile;
            var currentWriteTimeUtc = File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;

            if (currentWriteTimeUtc != _lastGeneratedTasksWriteTimeUtc)
            {
                RefreshGeneratedTasks();
            }
        }
    }

    private Hex1bWidget BuildRootWidget(RootContext ctx)
    {
        var prompt = _state.GetPrompt();
        if (prompt is not null)
        {
            return BuildPromptWidget(ctx, prompt);
        }

        var quitPrompt = _state.GetQuitPrompt();
        if (quitPrompt is not null)
        {
            return BuildQuitPromptWidget(ctx, quitPrompt);
        }

        var exitPrompt = _state.GetExitPrompt();
        var transcriptLines = _state.GetTranscriptLines(maxLines: CalculateTranscriptVisibleLines());
        var tasksSnapshot = _state.GetTasksSnapshot();
        var taskListVisibleRows = Math.Max(1, CalculateTaskListVisibleRows(tasksSnapshot.Tasks.Count));
        _state.EnsureTaskSelectionVisible(taskListVisibleRows);
        var infoItems = new List<string> { "Coralph", "TUI" };
        if (_options.DemoMode)
        {
            infoItems.Add("DEMO");
        }
        infoItems.AddRange(["Tasks", tasksSnapshot.Tasks.Count.ToString(), "Task Nav", "k/j  Ctrl+U/Ctrl+D  gg/Shift+G (Home/End)", "Keys", "Esc/q menu  Ctrl+C stops"]);
        var doneItems = new List<string> { "Coralph", "TUI" };
        if (_options.DemoMode)
        {
            doneItems.Add("DEMO");
        }
        doneItems.AddRange(["Done", "Enter/Esc/Q closes"]);

        return ctx.VStack(v =>
        [
            exitPrompt is null
                ? v.InfoBar(infoItems.ToArray()).FixedHeight(1)
                : v.InfoBar(doneItems.ToArray()).FixedHeight(1),
            v.Responsive(r =>
            [
                r.WhenMinWidth(130, w => w.HStack(h =>
                [
                    BuildTranscriptPane(h, transcriptLines).FillWidth(3),
                    BuildTasksPane(h, tasksSnapshot, taskListVisibleRows).FillWidth(2)
                ]).Fill()),
                r.Otherwise(w => w.VStack(stack =>
                [
                    BuildTranscriptPane(stack, transcriptLines).FillHeight(3),
                    BuildTasksPane(stack, tasksSnapshot, taskListVisibleRows).FillHeight(2)
                ]).Fill())
            ]).FillHeight()
        ]).WithInputBindings(bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.C).Global().Action(actionCtx =>
            {
                _state.CancelPrompt();
                _state.CancelQuitPrompt();
                ConsoleOutput.RequestStop();
                actionCtx.RequestStop();
            }, "Stop Coralph");

            if (exitPrompt is null)
            {
                bindings.Key(Hex1bKey.Escape).Global().Action(_ => OpenQuitPrompt(), "Open run menu");
                bindings.Key(Hex1bKey.Q).Global().Action(_ => OpenQuitPrompt(), "Open run menu");
            }

            if (exitPrompt is null && tasksSnapshot.Tasks.Count > 0)
            {
                var listRows = Math.Max(1, taskListVisibleRows);
                bindings.Key(Hex1bKey.K).Global().Action(_ => HandleTaskSelection(-1, listRows), "Previous task");
                bindings.Key(Hex1bKey.J).Global().Action(_ => HandleTaskSelection(1, listRows), "Next task");
                bindings.Ctrl().Key(Hex1bKey.U).Global().Action(_ => HandleTaskSelection(-listRows, listRows), "Previous task page");
                bindings.Ctrl().Key(Hex1bKey.D).Global().Action(_ => HandleTaskSelection(listRows, listRows), "Next task page");
                bindings.Key(Hex1bKey.G).Then().Key(Hex1bKey.G).Global().Action(_ => HandleTaskFirst(listRows), "First task");
                bindings.Shift().Key(Hex1bKey.G).Global().Action(_ => HandleTaskLast(listRows), "Last task");
                bindings.Key(Hex1bKey.Home).Global().Action(_ => HandleTaskFirst(listRows), "First task");
                bindings.Key(Hex1bKey.End).Global().Action(_ => HandleTaskLast(listRows), "Last task");
            }

            if (exitPrompt is not null)
            {
                bindings.Key(Hex1bKey.Enter).Global().Action(HandleExitPromptInput, "Close TUI");
                bindings.Key(Hex1bKey.Escape).Global().Action(HandleExitPromptInput, "Close TUI");
                bindings.Key(Hex1bKey.Q).Global().Action(HandleExitPromptInput, "Close TUI");
            }
        });
    }

    private Hex1bWidget BuildPromptWidget(RootContext ctx, PromptSelectionRequest prompt)
    {
        var options = prompt.Options.Count == 0 ? ["(no options)"] : prompt.Options;
        var promptList = (ctx.List(options) with
        {
            InitialSelectedIndex = Math.Clamp(prompt.SelectedIndex, 0, options.Count - 1)
        })
            .OnSelectionChanged(e => _state.UpdatePromptSelection(e.SelectedIndex))
            .OnItemActivated(e => _state.CompletePromptSelection(e.ActivatedIndex));

        return ctx.VStack(v =>
        [
            v.Text(prompt.Title),
            v.Text("Use arrows to choose, Enter to confirm.").FixedHeight(1),
            v.Border(promptList.Fill()).Title("Project Type").FillHeight(),
            v.InfoBar(["Prompt", "Active", "Keys", "Arrow + Enter"])
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Escape).Global().Action(_ => _state.CompletePromptSelection(prompt.SelectedIndex), "Confirm selection");
            bindings.Ctrl().Key(Hex1bKey.C).Global().Action(actionCtx =>
            {
                _state.CancelPrompt();
                _state.CancelQuitPrompt();
                ConsoleOutput.RequestStop();
                actionCtx.RequestStop();
            }, "Stop Coralph");
        });
    }

    private Hex1bWidget BuildQuitPromptWidget(RootContext ctx, QuitPromptRequest prompt)
    {
        var options = prompt.Options.Select(option => option.Label).ToArray();
        var promptList = (ctx.List(options) with
        {
            InitialSelectedIndex = Math.Clamp(prompt.SelectedIndex, 0, options.Length - 1)
        })
            .OnSelectionChanged(e => _state.UpdateQuitPromptSelection(e.SelectedIndex))
            .OnItemActivated(HandleQuitPromptActivated);

        return ctx.VStack(v =>
        [
            v.Text("Leave the TUI?"),
            v.Text("Choose how Coralph should continue.").FixedHeight(1),
            v.Border(promptList.Fill()).Title("Run Menu").FillHeight(),
            v.InfoBar(["Prompt", "Active", "Keys", "Arrow + Enter", "Esc", "Resume"])
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Escape).Global().Action(_ => CancelQuitPrompt(), "Resume TUI");
            bindings.Ctrl().Key(Hex1bKey.C).Global().Action(actionCtx =>
            {
                _state.CancelQuitPrompt();
                SignalExit(ConsoleOutputBackendExitReason.StopRequested, "Stop requested from TUI.");
                _cts.Cancel();
                ConsoleOutput.RequestStop();
                actionCtx.RequestStop();
            }, "Stop Coralph");
        });
    }

    private Hex1bWidget BuildTranscriptPane<TParent>(WidgetContext<TParent> ctx, IReadOnlyList<string> lines)
        where TParent : Hex1bWidget
    {
        var source = lines.Count == 0 ? ["Waiting for Coralph output..."] : lines;
        var transcriptText = string.Join(Environment.NewLine, source);
        return ctx.Border(ctx.WithClipping(ctx.Text(transcriptText).Fill())).Title("Run Transcript");
    }

    private Hex1bWidget BuildTasksPane<TParent>(WidgetContext<TParent> ctx, GeneratedTasksSnapshot snapshot, int visibleTaskRows)
        where TParent : Hex1bWidget
    {
        var contentWidth = CalculateTasksPaneContentWidth();
        var lines = BuildTasksPaneLines(snapshot, _state, visibleTaskRows, contentWidth, CalculateTaskPaneContentHeight());
        var tasksText = string.Join(Environment.NewLine, lines);
        return ctx.Border(ctx.WithClipping(ctx.Text(tasksText).Fill())).Title("Generated Tasks");
    }

    private void HandleTaskSelection(int delta, int visibleRows)
    {
        _state.MoveTaskSelection(delta, visibleRows);
        RequestInvalidate();
    }

    private void HandleTaskFirst(int visibleRows)
    {
        _state.SelectFirstTask(visibleRows);
        RequestInvalidate();
    }

    private void HandleTaskLast(int visibleRows)
    {
        _state.SelectLastTask(visibleRows);
        RequestInvalidate();
    }

    private static IEnumerable<string> BuildTaskBlock(
        GeneratedTaskSnapshotItem task,
        int width,
        bool isActive,
        bool includeDescription)
    {
        var status = string.IsNullOrWhiteSpace(task.Status) ? "open" : task.Status;
        var issuePrefix = task.IssueNumber > 0 ? $"#{task.IssueNumber} " : string.Empty;
        var marker = isActive ? ">>" : "  ";
        var headerText = $"{task.Id} [{status}] {issuePrefix}{task.Title}".Trim();

        var lines = new List<string>(WrapWithPrefix($"{marker} ", headerText, width));
        if (!includeDescription)
        {
            return lines;
        }

        var description = string.IsNullOrWhiteSpace(task.Description) ? "No description" : task.Description;
        lines.AddRange(IndentLines(WrapTextBlock(description, Math.Max(10, width - 2)), "  "));
        return lines;
    }

    internal static IReadOnlyList<string> BuildTasksPaneLines(
        GeneratedTasksSnapshot snapshot,
        TuiState state,
        int visibleTaskRows,
        int contentWidth,
        int contentHeight)
    {
        var lines = new List<string>();
        lines.AddRange(WrapWithPrefix("Model: ", state.GetSelectedModel() ?? "unknown", contentWidth));
        lines.Add(string.Empty);

        if (!snapshot.Exists)
        {
            lines.Add("generated_tasks.json not found");
            lines.Add(snapshot.Path);
            return lines;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Error))
        {
            lines.Add("Unable to read generated tasks");
            lines.Add(snapshot.Error);
            return lines;
        }

        if (snapshot.Tasks.Count == 0)
        {
            lines.Add("No generated tasks.");
            lines.Add("Backlog is empty or still loading.");
            return lines;
        }

        var activeTaskIndex = state.GetTaskSelectedIndex(snapshot.ActiveTaskIndex());
        if (activeTaskIndex < 0 || activeTaskIndex >= snapshot.Tasks.Count)
        {
            activeTaskIndex = snapshot.ActiveTaskIndex();
            state.SetTaskSelectedIndex(activeTaskIndex);
        }

        var activeTask = snapshot.Tasks[activeTaskIndex];
        var clampedVisibleRows = Math.Max(1, Math.Min(visibleTaskRows, snapshot.Tasks.Count));
        var scrollOffset = Math.Clamp(
            state.GetTaskListScrollOffset(),
            0,
            Math.Max(0, snapshot.Tasks.Count - clampedVisibleRows));
        lines.Add("Current Task");
        var activeTaskLines = BuildTaskBlock(activeTask, contentWidth, isActive: true, includeDescription: true).ToList();
        var maxCurrentTaskLines = Math.Max(1, contentHeight / 3);
        var visibleCurrentTaskLines = activeTaskLines.Take(maxCurrentTaskLines).ToList();

        lines.AddRange(visibleCurrentTaskLines);
        if (activeTaskLines.Count > visibleCurrentTaskLines.Count)
        {
            lines.Add("  ...");
        }
        lines.Add(string.Empty);
        lines.Add("All Tasks");

        var startIndex = Math.Max(0, scrollOffset);
        var endIndex = Math.Min(snapshot.Tasks.Count, startIndex + clampedVisibleRows);

        for (var i = startIndex; i < endIndex; i++)
        {
            lines.AddRange(BuildTaskBlock(snapshot.Tasks[i], contentWidth, isActive: i == activeTaskIndex, includeDescription: false));
            if (i < endIndex - 1)
            {
                lines.Add(string.Empty);
            }
        }

        return lines;
    }

    private static IEnumerable<string> WrapTextBlock(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>();
        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            lines.AddRange(WrapTextLine(trimmed, width));
        }

        return lines;
    }

    private static IEnumerable<string> WrapTextLine(string line, int width)
    {
        if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
        {
            var prefix = line[..2];
            var content = line[2..];
            return WrapWithPrefix(prefix, content, width);
        }

        return WrapPlainText(line, width);
    }

    private static IEnumerable<string> WrapWithPrefix(string prefix, string text, int width)
    {
        var availableWidth = Math.Max(1, width - prefix.Length);
        var wrapped = WrapPlainText(text, availableWidth);
        var result = new List<string>();
        var indent = new string(' ', prefix.Length);
        var index = 0;
        foreach (var line in wrapped)
        {
            result.Add((index++ == 0 ? prefix : indent) + line);
        }

        return result;
    }

    private static IEnumerable<string> WrapPlainText(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (word.Length > width)
            {
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                for (var i = 0; i < word.Length; i += width)
                {
                    lines.Add(word.Substring(i, Math.Min(width, word.Length - i)));
                }

                continue;
            }

            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length <= width)
            {
                current.Append(' ').Append(word);
            }
            else
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    private static IEnumerable<string> IndentLines(IEnumerable<string> lines, string indent)
    {
        foreach (var line in lines)
        {
            yield return string.IsNullOrEmpty(line) ? string.Empty : indent + line;
        }
    }

    private static int CalculateTasksPaneContentWidth()
    {
        var width = ReadConsoleDimension(() => Console.WindowWidth, fallback: 120);
        var paneWidth = width >= 130 ? Math.Max(20, (width * 2) / 5) : width;
        return Math.Max(20, paneWidth - 2);
    }

    private void HandleExitPromptInput(InputBindingActionContext actionCtx)
    {
        _state.CompleteExitPrompt();
        SignalExit(ConsoleOutputBackendExitReason.Completed, "TUI closed after completion.");
        _cts.Cancel();
        actionCtx.RequestStop();
    }

    private static int CalculateTranscriptVisibleLines()
    {
        var width = ReadConsoleDimension(() => Console.WindowWidth, fallback: 120);
        var height = ReadConsoleDimension(() => Console.WindowHeight, fallback: 40);

        var bodyHeight = Math.Max(1, height - 1); // Info bar.
        var transcriptPaneHeight = width >= 130
            ? bodyHeight
            : Math.Max(1, (bodyHeight * 3) / 5); // FillHeight(3) vs FillHeight(2)

        // Border consumes top and bottom rows.
        return Math.Max(1, transcriptPaneHeight - 2);
    }

    private static int CalculateTaskListVisibleRows(int taskCount)
    {
        if (taskCount <= 0)
        {
            return 1;
        }

        var paneHeight = CalculateTaskPaneContentHeight();
        var reservedForCurrentTask = Math.Max(1, paneHeight / 3);
        var availableRows = paneHeight - reservedForCurrentTask - 2;
        return Math.Clamp(availableRows, 1, taskCount);
    }

    private static int CalculateTaskPaneContentHeight()
    {
        var width = ReadConsoleDimension(() => Console.WindowWidth, fallback: 120);
        var height = ReadConsoleDimension(() => Console.WindowHeight, fallback: 40);
        var bodyHeight = Math.Max(1, height - 1); // Info bar.
        var paneHeight = width >= 130
            ? bodyHeight
            : Math.Max(1, (bodyHeight * 2) / 5);

        return Math.Max(1, paneHeight - 2);
    }

    private static int ReadConsoleDimension(Func<int> reader, int fallback)
    {
        try
        {
            var value = reader();
            return value > 0 ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void RequestInvalidate()
    {
        try
        {
            _app?.Invalidate();
        }
        catch
        {
            // Best effort; app may be shutting down.
        }
    }

    private void OpenQuitPrompt()
    {
        _state.OpenQuitPrompt();
        RequestInvalidate();
    }

    private void CancelQuitPrompt()
    {
        _state.CancelQuitPrompt();
        RequestInvalidate();
    }

    private void HandleQuitPromptActivated(ListItemActivatedEventArgs activated)
    {
        var action = _state.CompleteQuitPrompt(activated.ActivatedIndex);
        RequestInvalidate();

        switch (action)
        {
            case TuiQuitAction.ResumeTui:
                return;
            case TuiQuitAction.SwitchToClassic:
                SignalExit(ConsoleOutputBackendExitReason.SwitchToClassic, "Leaving TUI. Continuing in classic output.");
                _cts.Cancel();
                activated.Context.RequestStop();
                return;
            case TuiQuitAction.StopCoralph:
                SignalExit(ConsoleOutputBackendExitReason.StopRequested, "Stop requested from TUI.");
                _cts.Cancel();
                ConsoleOutput.RequestStop();
                activated.Context.RequestStop();
                return;
            default:
                return;
        }
    }

    private void SignalExit(ConsoleOutputBackendExitReason reason, string? message = null, Exception? exception = null)
    {
        if (Interlocked.Exchange(ref _exitSignaled, 1) != 0)
        {
            return;
        }

        _exitTcs.TrySetResult(new ConsoleOutputBackendExit(reason, message, exception));
    }

    private static string StripMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return MarkupRegex.Replace(text, string.Empty);
    }
}
