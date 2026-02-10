using System.Diagnostics;
using Coralph;

namespace Coralph.Tests;

[Collection("InitWorkflowSerial")]
public sealed class WorkingDirectoryContextTests : IDisposable
{
    private readonly string _originalWorkingDirectory;
    private readonly List<string> _tempDirectories = [];

    public WorkingDirectoryContextTests()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
    }

    [Fact]
    public void TryResolveRepoRoot_WithRepositoryRoot_Succeeds()
    {
        var repoRoot = CreateTempDirectory("coralph-working-dir-root");
        InitializeGitRepository(repoRoot);
        var expectedRepoRoot = GetGitRepositoryRoot(repoRoot);

        var ok = WorkingDirectoryContext.TryResolveRepoRoot(
            repoRoot,
            Directory.GetCurrentDirectory(),
            out var resolvedRepoRoot,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(expectedRepoRoot, resolvedRepoRoot);
    }

    [Fact]
    public void TryResolveRepoRoot_WithNestedPath_ReturnsTopLevelRoot()
    {
        var repoRoot = CreateTempDirectory("coralph-working-dir-nested");
        InitializeGitRepository(repoRoot);
        var expectedRepoRoot = GetGitRepositoryRoot(repoRoot);
        var nestedPath = Path.Combine(repoRoot, "src", "feature");
        Directory.CreateDirectory(nestedPath);

        var ok = WorkingDirectoryContext.TryResolveRepoRoot(
            nestedPath,
            Directory.GetCurrentDirectory(),
            out var resolvedRepoRoot,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(expectedRepoRoot, resolvedRepoRoot);
    }

    [Fact]
    public void TryResolveRepoRoot_WithMissingDirectory_Fails()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"coralph-working-dir-missing-{Guid.NewGuid():N}");

        var ok = WorkingDirectoryContext.TryResolveRepoRoot(
            missingPath,
            Directory.GetCurrentDirectory(),
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("does not exist", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveRepoRoot_WithNonGitDirectory_Fails()
    {
        var nonGitPath = CreateTempDirectory("coralph-working-dir-non-git");

        var ok = WorkingDirectoryContext.TryResolveRepoRoot(
            nonGitPath,
            Directory.GetCurrentDirectory(),
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("not inside a git repository", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryApply_OnFailure_DoesNotChangeCurrentDirectory()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var nonGitPath = CreateTempDirectory("coralph-working-dir-apply-fail");

        var ok = WorkingDirectoryContext.TryApply(nonGitPath, out _, out _);

        Assert.False(ok);
        Assert.Equal(originalCwd, Directory.GetCurrentDirectory());
    }

    [Fact]
    public void TryApply_OnSuccess_SetsCurrentDirectoryToRepoRoot()
    {
        var repoRoot = CreateTempDirectory("coralph-working-dir-apply-success");
        InitializeGitRepository(repoRoot);
        var expectedRepoRoot = GetGitRepositoryRoot(repoRoot);
        var nestedPath = Path.Combine(repoRoot, "services", "api");
        Directory.CreateDirectory(nestedPath);

        var ok = WorkingDirectoryContext.TryApply(nestedPath, out var resolvedRepoRoot, out var error);

        Assert.True(ok, error);
        Assert.Equal(expectedRepoRoot, resolvedRepoRoot);
        Assert.Equal(resolvedRepoRoot, Directory.GetCurrentDirectory());
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
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repoRoot
        };
        psi.ArgumentList.Add("init");
        psi.ArgumentList.Add("--quiet");

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git init failed in {repoRoot}. stdout: {stdout}; stderr: {stderr}");
    }

    private static string GetGitRepositoryRoot(string path)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(path);
        psi.ArgumentList.Add("rev-parse");
        psi.ArgumentList.Add("--show-toplevel");

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git rev-parse failed in {path}. stdout: {stdout}; stderr: {stderr}");
        return stdout.Trim();
    }
}
