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
