using System.Diagnostics;
using Coralph;

namespace Coralph.Tests;

[Collection("InitWorkflowSerial")]
public sealed class GitServiceTests : IDisposable
{
    private readonly string _originalWorkingDirectory;
    private readonly List<string> _tempDirectories = [];

    public GitServiceTests()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
    }

    [Fact]
    public async Task RunGitAsync_WithValidCommand_ReturnsTrimmedOutput()
    {
        var repoRoot = CreateTempDirectory("coralph-gitservice-run");
        InitializeGitRepository(repoRoot);
        Directory.SetCurrentDirectory(repoRoot);
        var expectedRepoRoot = GetGitRepositoryRoot(repoRoot);

        var output = await GitService.RunGitAsync(["rev-parse", "--show-toplevel"], CancellationToken.None);

        Assert.Equal(expectedRepoRoot, output);
    }

    [Fact]
    public async Task CommitProgressIfNeededAsync_WithMissingFile_DoesNothing()
    {
        var repoRoot = CreateTempDirectory("coralph-gitservice-missing");
        InitializeGitRepository(repoRoot);
        Directory.SetCurrentDirectory(repoRoot);

        await GitService.CommitProgressIfNeededAsync("missing-progress.txt", CancellationToken.None);

        var status = await GitService.RunGitAsync(["status", "--porcelain"], CancellationToken.None);
        Assert.True(string.IsNullOrWhiteSpace(status));
    }

    [Fact]
    public async Task CommitProgressIfNeededAsync_WithDirtyProgressFile_CreatesCommit()
    {
        var repoRoot = CreateTempDirectory("coralph-gitservice-commit");
        InitializeGitRepository(repoRoot);
        ConfigureGitIdentity(repoRoot);
        Directory.SetCurrentDirectory(repoRoot);

        var progressFile = Path.Combine(repoRoot, "progress.txt");
        await File.WriteAllTextAsync(progressFile, "updated progress");

        await GitService.CommitProgressIfNeededAsync(progressFile, CancellationToken.None);

        var status = await GitService.RunGitAsync(["status", "--porcelain"], CancellationToken.None);
        var commitMessage = await GitService.RunGitAsync(["log", "-1", "--pretty=%s"], CancellationToken.None);

        Assert.True(string.IsNullOrWhiteSpace(status));
        Assert.Equal("chore: update progress.txt", commitMessage);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalWorkingDirectory);

        foreach (var directory in _tempDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private static void InitializeGitRepository(string repoRoot)
    {
        RunGit(repoRoot, "init", "--quiet");
    }

    private static void ConfigureGitIdentity(string repoRoot)
    {
        RunGit(repoRoot, "config", "user.name", "Coralph Tests");
        RunGit(repoRoot, "config", "user.email", "coralph-tests@example.com");
    }

    private static string GetGitRepositoryRoot(string repoRoot)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repoRoot);
        psi.ArgumentList.Add("rev-parse");
        psi.ArgumentList.Add("--show-toplevel");

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git rev-parse failed. stdout: {stdout}; stderr: {stderr}");
        return stdout.Trim();
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed. stdout: {stdout}; stderr: {stderr}");
    }
}
