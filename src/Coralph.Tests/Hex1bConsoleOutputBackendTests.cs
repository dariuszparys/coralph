using System.Reflection;
using Coralph.Ui.Tui;

namespace Coralph.Tests;

public sealed class Hex1bConsoleOutputBackendTests : IDisposable
{
    private readonly string _tempDir;

    public Hex1bConsoleOutputBackendTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"coralph-hex1b-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void RefreshGeneratedTasks_DoesNotReloadWhenTimestampIsUnchanged()
    {
        var path = Path.Combine(_tempDir, "generated_tasks.json");
        File.WriteAllText(path, "[]");

        var backend = new Hex1bConsoleOutputBackend(new LoopOptions
        {
            GeneratedTasksFile = path
        });

        backend.RefreshGeneratedTasks();

        var state = GetState(backend);
        var firstSnapshot = state.GetTasksSnapshot();

        backend.RefreshGeneratedTasks();

        var secondSnapshot = state.GetTasksSnapshot();

        Assert.Same(firstSnapshot, secondSnapshot);
    }

    [Fact]
    public void BuildTasksPaneLines_PutsSelectedModelAtTop()
    {
        var state = new TuiState();
        state.SetSelectedModel("gpt-5.4");
        state.SetTasksSnapshot(new GeneratedTasksSnapshot(
            Path: "generated_tasks.json",
            Exists: true,
            Error: null,
            Tasks:
            [
                new GeneratedTaskSnapshotItem("126-001", 126, "Show model name", "Display the selected model in the info pane.", "in_progress", 1, 1)
            ],
            ReadAtUtc: DateTimeOffset.UtcNow));

        var lines = Hex1bConsoleOutputBackend.BuildTasksPaneLines(state.GetTasksSnapshot(), state, visibleTaskRows: 3, contentWidth: 40, contentHeight: 18);

        Assert.Equal("Model: gpt-5.4", lines[0]);
        Assert.Equal("Current Task", lines[2]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static TuiState GetState(Hex1bConsoleOutputBackend backend)
    {
        var field = typeof(Hex1bConsoleOutputBackend).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return (TuiState)field!.GetValue(backend)!;
    }
}
