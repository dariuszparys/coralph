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
        var snapshot = new GeneratedTasksSnapshot(
            Path: "generated_tasks.json",
            Exists: true,
            Error: null,
            Tasks:
            [
                new GeneratedTaskSnapshotItem("001", 1, "Open task", "", "open", 1, 1),
                new GeneratedTaskSnapshotItem("002", 1, "Active task", "", "in_progress", 2, 2),
                new GeneratedTaskSnapshotItem("003", 1, "Done task", "", "done", 3, 3)
            ],
            ReadAtUtc: DateTimeOffset.UtcNow);

        state.SetTasksSnapshot(snapshot);

        Assert.Equal(1, state.GetTaskSelectedIndex(-1));
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
}
