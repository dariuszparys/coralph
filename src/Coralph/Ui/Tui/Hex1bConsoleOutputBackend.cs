using System.Text.RegularExpressions;
using Coralph.Ui;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;
using Spectre.Console;

namespace Coralph.Ui.Tui;

internal sealed class Hex1bConsoleOutputBackend : IConsoleOutputBackend
{
    private static readonly Regex MarkupRegex = new(@"\[[^\]]+\]", RegexOptions.Compiled);
    private static readonly Hex1bKey[] ExitAnyKeys = Enum.GetValues<Hex1bKey>()
        .Where(key => key != Hex1bKey.None)
        .ToArray();

    private readonly TuiState _state = new();
    private readonly GeneratedTasksSnapshotReader _tasksReader = new();
    private readonly LoopOptions _options;
    private readonly CancellationTokenSource _cts = new();

    private Hex1bApp? _app;
    private Task? _uiTask;
    private Task? _pollTask;

    private IAnsiConsole _out;
    private IAnsiConsole _error;

    public Hex1bConsoleOutputBackend(LoopOptions options)
    {
        _options = options;
        _out = ConsoleOutput.CreateConsole(Console.Out, Console.IsOutputRedirected);
        _error = ConsoleOutput.CreateConsole(Console.Error, Console.IsErrorRedirected);
    }

    public bool UsesTui => true;

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
        _state.CancelPrompt();
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
        _state.AppendLine(TranscriptEntryKind.Tool, $"[start] {toolName}");
        RequestInvalidate();
    }

    public void WriteToolComplete(string toolName, string summary)
    {
        _state.AppendLine(TranscriptEntryKind.Tool, $"[done] {toolName}: {summary}");
        RequestInvalidate();
    }

    public void WriteSectionSeparator(string title)
    {
        _state.AppendLine(TranscriptEntryKind.System, $"--- {title} ---");
        RequestInvalidate();
    }

    public void RefreshGeneratedTasks()
    {
        var snapshot = _tasksReader.Read(_options.GeneratedTasksFile);
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
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
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

            RefreshGeneratedTasks();
        }
    }

    private Hex1bWidget BuildRootWidget(RootContext ctx)
    {
        var prompt = _state.GetPrompt();
        if (prompt is not null)
        {
            return BuildPromptWidget(ctx, prompt);
        }

        var exitPrompt = _state.GetExitPrompt();
        var transcriptLines = _state.GetTranscriptLines(maxLines: 180);
        var tasksSnapshot = _state.GetTasksSnapshot();

        return ctx.VStack(v =>
        [
            exitPrompt is null
                ? v.InfoBar(
                    [
                        "Coralph",
                        "TUI",
                        "Tasks",
                        tasksSnapshot.Tasks.Count.ToString(),
                        "Follow",
                        _state.IsTranscriptFollowEnabled() ? "ON" : "OFF",
                        "Keys",
                        "Arrows scroll, End follows, Ctrl+C exits"
                    ]).FixedHeight(1)
                : v.InfoBar(["Coralph", "TUI", "Done", "Press any key to exit"]).FixedHeight(1),
            v.Responsive(r =>
            [
                r.WhenMinWidth(130, w => w.HStack(h =>
                [
                    BuildTranscriptPane(h, transcriptLines).FillWidth(3),
                    BuildTasksPane(h, tasksSnapshot).FillWidth(2)
                ]).Fill()),
                r.Otherwise(w => w.VStack(stack =>
                [
                    BuildTranscriptPane(stack, transcriptLines).FillHeight(3),
                    BuildTasksPane(stack, tasksSnapshot).FillHeight(2)
                ]).Fill())
            ]).FillHeight()
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.End).Global().Action(_ =>
            {
                var latestIndex = Math.Max(0, transcriptLines.Count - 1);
                _state.SetTranscriptSelectedIndex(latestIndex, latestIndex);
            }, "Follow transcript");

            bindings.Ctrl().Key(Hex1bKey.C).Global().Action(actionCtx =>
            {
                _state.CancelPrompt();
                ConsoleOutput.RequestStop();
                actionCtx.RequestStop();
            }, "Stop Coralph");

            if (exitPrompt is not null)
            {
                foreach (var key in ExitAnyKeys)
                {
                    bindings.Key(key).Global().Action(HandleExitPromptInput, "Close TUI");
                }
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
                ConsoleOutput.RequestStop();
                actionCtx.RequestStop();
            }, "Stop Coralph");
        });
    }

    private Hex1bWidget BuildTranscriptPane<TParent>(WidgetContext<TParent> ctx, IReadOnlyList<string> lines)
        where TParent : Hex1bWidget
    {
        var source = lines.Count == 0 ? ["Waiting for Coralph output..."] : lines;
        var followEnabled = _state.IsTranscriptFollowEnabled();

        var transcriptList = (ctx.List(source) with
        {
            InitialSelectedIndex = Math.Clamp(_state.GetTranscriptSelectedIndex(source.Count - 1), 0, source.Count - 1)
        }).OnSelectionChanged(e => _state.SetTranscriptSelectedIndex(e.SelectedIndex, source.Count - 1));

        if (!followEnabled)
        {
            return ctx.Border(transcriptList.Fill()).Title("Run Transcript");
        }

        // ListWidget preserves selection state across reconciliation. While follow mode is enabled,
        // alternate wrappers on transcript updates so the list re-initializes at the latest row.
        var revision = _state.GetTranscriptRevision();
        return (revision & 1) == 0
            ? ctx.Border(transcriptList.Fill()).Title("Run Transcript")
            : ctx.Border(ctx.WithClipping(transcriptList.Fill())).Title("Run Transcript");
    }

    private Hex1bWidget BuildTasksPane<TParent>(WidgetContext<TParent> ctx, GeneratedTasksSnapshot snapshot)
        where TParent : Hex1bWidget
    {
        if (!snapshot.Exists)
        {
            return ctx.Border(ctx.VStack(v =>
            [
                v.Text("generated_tasks.json not found"),
                v.Text(snapshot.Path)
            ])).Title("Generated Tasks");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Error))
        {
            return ctx.Border(ctx.VStack(v =>
            [
                v.Text("Unable to read generated tasks"),
                v.Text(snapshot.Error)
            ])).Title("Generated Tasks");
        }

        if (snapshot.Tasks.Count == 0)
        {
            return ctx.Border(ctx.VStack(v =>
            [
                v.Text("No generated tasks."),
                v.Text("Backlog is empty or still loading.")
            ])).Title("Generated Tasks");
        }

        var activeIndex = snapshot.ActiveTaskIndex();
        var labels = snapshot.Tasks
            .Select((task, index) => FormatTaskLabel(task, index == activeIndex))
            .ToArray();

        var tasksList = (ctx.List(labels) with
        {
            InitialSelectedIndex = Math.Clamp(_state.GetTaskSelectedIndex(activeIndex), 0, labels.Length - 1)
        })
            .OnSelectionChanged(e => _state.SetTaskSelectedIndex(e.SelectedIndex))
            .OnItemActivated(e => _state.SetTaskSelectedIndex(e.ActivatedIndex));

        var selectedIndex = Math.Clamp(_state.GetTaskSelectedIndex(activeIndex), 0, snapshot.Tasks.Count - 1);
        var selectedTask = snapshot.Tasks[selectedIndex];
        var detailsText = string.IsNullOrWhiteSpace(selectedTask.Description)
            ? "No description"
            : selectedTask.Description.Replace("\r\n", " ").Replace('\n', ' ');

        return ctx.Border(ctx.VStack(v =>
        [
            tasksList.FillHeight(4),
            v.Text($"Current: {selectedTask.Id} ({selectedTask.Status})"),
            v.Text(detailsText)
        ])).Title("Generated Tasks");
    }

    private static string FormatTaskLabel(GeneratedTaskSnapshotItem task, bool isActive)
    {
        var marker = isActive ? "*" : " ";
        var issuePrefix = task.IssueNumber > 0 ? $"#{task.IssueNumber} " : string.Empty;
        return $"{marker} [{task.Status}] {task.Id} {issuePrefix}{task.Title}";
    }

    private void HandleExitPromptInput(InputBindingActionContext actionCtx)
    {
        _state.CompleteExitPrompt();
        actionCtx.RequestStop();
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

    private static string StripMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return MarkupRegex.Replace(text, string.Empty);
    }
}
