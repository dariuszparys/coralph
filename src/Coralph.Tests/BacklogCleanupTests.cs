using Coralph;

namespace Coralph.Tests;

public class BacklogCleanupTests : IDisposable
{
    private readonly string _tempDir;

    public BacklogCleanupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"coralph-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Theory]
    [InlineData("COMPLETE")]
    [InlineData("ALL_TASKS_COMPLETE")]
    [InlineData("NO_OPEN_ISSUES")]
    [InlineData("complete")]
    public void ShouldDeleteForTerminalSignal_WithCompletionSignals_ReturnsTrue(string signal)
    {
        var result = BacklogCleanup.ShouldDeleteForTerminalSignal(signal);

        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("MAX_ITERATIONS_REACHED")]
    [InlineData("CONTINUE")]
    public void ShouldDeleteForTerminalSignal_WithNonCompletionSignals_ReturnsFalse(string signal)
    {
        var result = BacklogCleanup.ShouldDeleteForTerminalSignal(signal);

        Assert.False(result);
    }

    [Fact]
    public async Task TryDelete_WithExistingFile_DeletesFile()
    {
        var backlogFile = Path.Combine(_tempDir, "generated_tasks.json");
        await File.WriteAllTextAsync(backlogFile, "{}");

        var deleted = BacklogCleanup.TryDelete(backlogFile, out var error);

        Assert.True(deleted);
        Assert.Null(error);
        Assert.False(File.Exists(backlogFile));
    }

    [Fact]
    public void TryDelete_WithMissingFile_ReturnsFalseWithoutError()
    {
        var backlogFile = Path.Combine(_tempDir, "missing-generated_tasks.json");

        var deleted = BacklogCleanup.TryDelete(backlogFile, out var error);

        Assert.False(deleted);
        Assert.Null(error);
    }
}
