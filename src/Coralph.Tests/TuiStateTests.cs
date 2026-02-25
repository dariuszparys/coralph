using Coralph.Ui.Tui;

namespace Coralph.Tests;

public class TuiStateTests
{
    [Fact]
    public void AppendChunk_CoalescesSameKind()
    {
        var state = new TuiState();

        state.AppendChunk(TranscriptEntryKind.Assistant, "Hel");
        state.AppendChunk(TranscriptEntryKind.Assistant, "lo");

        var lines = state.GetTranscriptLines(10);

        Assert.Single(lines);
        Assert.Contains("Hello", lines[0]);
    }

    [Fact]
    public void SetTasksSnapshot_SelectsInProgressFirst()
    {
        var state = new TuiState();
        var snapshot = BuildSnapshotWithStatuses(["open", "in_progress", "done"]);
        state.SetTasksSnapshot(snapshot);

        Assert.Equal(1, state.GetTaskSelectedIndex(-1));
    }

    [Fact]
    public void SetTasksSnapshot_ClampsSelectionAndScrollOnCountChange()
    {
        var state = new TuiState();
        var initial = BuildSnapshotWithStatuses(["open", "open", "in_progress", "open", "open"]);
        state.SetTasksSnapshot(initial);
        state.SetTaskSelectedIndex(4);
        state.SetTaskListScrollOffset(4);
        state.EnsureTaskSelectionVisible(3);

        var shrunk = BuildSnapshotWithStatuses(["done", "in_progress"]);
        state.SetTasksSnapshot(shrunk);

        Assert.Equal(1, state.GetTaskSelectedIndex(-1));
        Assert.Equal(1, state.GetTaskListScrollOffset());

        var expanded = BuildSnapshotWithStatuses(["open", "open", "open", "open", "open", "open", "open"]);
        state.SetTasksSnapshot(expanded);

        Assert.Equal(1, state.GetTaskSelectedIndex(-1));
        Assert.Equal(1, state.GetTaskListScrollOffset());
    }

    [Fact]
    public void SetTaskListScrollOffset_ClampsForEmptySmallAndLargeCollections()
    {
        var state = new TuiState();

        state.SetTasksSnapshot(BuildSnapshotWithStatuses(Array.Empty<string>()));
        state.SetTaskListScrollOffset(10, visibleRows: 5);
        Assert.Equal(0, state.GetTaskListScrollOffset());
        Assert.Equal(-1, state.GetTaskSelectedIndex(-1));

        state.SetTasksSnapshot(BuildSnapshotWithStatuses(["open", "open"]));
        state.SetTaskListScrollOffset(10, visibleRows: 5);
        Assert.Equal(0, state.GetTaskListScrollOffset());

        state.SetTasksSnapshot(BuildSnapshotWithStatuses(
        [
            "open", "open", "open", "open", "open", "open", "open", "open", "open", "open"
        ]));
        state.SetTaskListScrollOffset(10, visibleRows: 5);
        Assert.Equal(5, state.GetTaskListScrollOffset());
        state.SetTaskListScrollOffset(-7, visibleRows: 5);
        Assert.Equal(0, state.GetTaskListScrollOffset());
    }

    [Fact]
    public void TaskSelectionNavigation_UsesViewportForScrollOffsets()
    {
        var state = new TuiState();
        state.SetTasksSnapshot(BuildSnapshotWithStatuses(["open", "open", "open", "open", "open", "open", "open", "open", "open", "open"]));

        state.SelectFirstTask(3);
        Assert.Equal(0, state.GetTaskSelectedIndex(-1));
        Assert.Equal(0, state.GetTaskListScrollOffset());

        state.MoveTaskSelection(9, 3);
        Assert.Equal(9, state.GetTaskSelectedIndex(-1));
        Assert.Equal(7, state.GetTaskListScrollOffset());

        state.MoveTaskSelection(-1, 3);
        Assert.Equal(8, state.GetTaskSelectedIndex(-1));
        Assert.Equal(7, state.GetTaskListScrollOffset());

        state.MoveTaskSelection(-2, 3);
        Assert.Equal(6, state.GetTaskSelectedIndex(-1));
        Assert.Equal(6, state.GetTaskListScrollOffset());

        state.SelectFirstTask(3);
        Assert.Equal(0, state.GetTaskSelectedIndex(-1));
        Assert.Equal(0, state.GetTaskListScrollOffset());

        state.SelectLastTask(3);
        Assert.Equal(9, state.GetTaskSelectedIndex(-1));
        Assert.Equal(7, state.GetTaskListScrollOffset());
    }

    [Fact]
    public void ActiveTaskIndex_FallsBackToOpen()
    {
        var snapshot = new GeneratedTasksSnapshot(
            Path: "generated_tasks.json",
            Exists: true,
            Error: null,
            Tasks:
            [
                new GeneratedTaskSnapshotItem("001", 1, "Done", "", "done", 1, 1),
                new GeneratedTaskSnapshotItem("002", 1, "Open", "", "open", 2, 2)
            ],
            ReadAtUtc: DateTimeOffset.UtcNow);

        Assert.Equal(1, snapshot.ActiveTaskIndex());
    }

    [Fact]
    public void SnapshotReader_MissingAndInvalidFilesHandled()
    {
        var reader = new GeneratedTasksSnapshotReader();
        var tempDir = Path.Combine(Path.GetTempPath(), $"coralph-tui-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var missingPath = Path.Combine(tempDir, "missing.json");
            var missing = reader.Read(missingPath);
            Assert.False(missing.Exists);
            Assert.NotNull(missing.Error);

            var invalidPath = Path.Combine(tempDir, "invalid.json");
            File.WriteAllText(invalidPath, "{ bad json");
            var invalid = reader.Read(invalidPath);
            Assert.True(invalid.Exists);
            Assert.NotNull(invalid.Error);
            Assert.Empty(invalid.Tasks);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp files.
            }
        }
    }

    [Fact]
    public async Task WaitForAnyKeyAsync_CompletesWhenPromptHandled()
    {
        var state = new TuiState();

        var waitTask = state.WaitForAnyKeyAsync("No work remains.");
        var prompt = state.GetExitPrompt();

        Assert.NotNull(prompt);
        Assert.Equal("No work remains.", prompt.Message);

        state.CompleteExitPrompt();

        await waitTask;
        Assert.Null(state.GetExitPrompt());
    }

    [Fact]
    public void TranscriptLines_ReturnsMostRecentWhenMaxExceeded()
    {
        var state = new TuiState();
        for (var i = 0; i < 5; i++)
        {
            state.AppendLine(TranscriptEntryKind.System, $"line-{i}");
        }

        var lines = state.GetTranscriptLines(maxLines: 2);

        Assert.Equal(2, lines.Count);
        Assert.Contains("line-3", lines[0]);
        Assert.Contains("line-4", lines[1]);
    }

    private static GeneratedTasksSnapshot BuildSnapshotWithStatuses(string[] statuses)
    {
        return new GeneratedTasksSnapshot(
            Path: "generated_tasks.json",
            Exists: true,
            Error: null,
            Tasks:
            [
                .. statuses.Select((status, index) => new GeneratedTaskSnapshotItem(
                    ((index + 1).ToString("000")),
                    index + 1,
                    $"Task {index + 1}",
                    $"Description for task {index + 1}",
                    status,
                    index + 1,
                    index + 1))
            ],
            ReadAtUtc: DateTimeOffset.UtcNow);
    }
}
