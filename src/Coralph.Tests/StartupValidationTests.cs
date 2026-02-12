using Coralph;

namespace Coralph.Tests;

public sealed class StartupValidationTests : IDisposable
{
    private readonly string _originalWorkingDirectory;
    private readonly List<string> _tempDirectories = [];

    public StartupValidationTests()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
    }

    [Fact]
    public async Task TryValidatePromptFile_WhenPromptExists_ReturnsTrue()
    {
        var repoRoot = CreateTempDirectory("coralph-startup-validation-ok");
        var promptPath = Path.Combine(repoRoot, "prompt.md");
        await File.WriteAllTextAsync(promptPath, "# Prompt");

        var isValid = StartupValidation.TryValidatePromptFile(promptPath, out var errorMessage);

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryValidatePromptFile_WhenPromptMissing_ReturnsFalseWithInitGuidance()
    {
        var repoRoot = CreateTempDirectory("coralph-startup-validation-missing");
        var promptPath = Path.Combine(repoRoot, "prompt.md");

        var isValid = StartupValidation.TryValidatePromptFile(promptPath, out var errorMessage);

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("Prompt file not found:", errorMessage, StringComparison.Ordinal);
        Assert.Contains("coralph --init", errorMessage, StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(promptPath), errorMessage, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalWorkingDirectory);

        foreach (var path in _tempDirectories.Where(Directory.Exists))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private string CreateTempDirectory(string prefix)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        _tempDirectories.Add(tempDirectory);
        return tempDirectory;
    }
}
