using System.Diagnostics;

namespace Coralph;

internal static class WorkingDirectoryContext
{
    internal static bool TryApply(string requestedWorkingDir, out string repoRoot, out string error)
    {
        repoRoot = string.Empty;
        error = string.Empty;

        string launchDirectory;
        try
        {
            launchDirectory = Directory.GetCurrentDirectory();
        }
        catch (DirectoryNotFoundException ex)
        {
            error = $"Current working directory is unavailable: {ex.Message}";
            return false;
        }
        catch (IOException ex)
        {
            error = $"Current working directory is unavailable: {ex.Message}";
            return false;
        }

        if (!TryResolveRepoRoot(requestedWorkingDir, launchDirectory, out var resolvedRepoRoot, out error))
        {
            return false;
        }

        try
        {
            Directory.SetCurrentDirectory(resolvedRepoRoot);
        }
        catch (DirectoryNotFoundException ex)
        {
            error = $"Failed to switch to working directory '{resolvedRepoRoot}': {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"Failed to switch to working directory '{resolvedRepoRoot}': {ex.Message}";
            return false;
        }
        catch (IOException ex)
        {
            error = $"Failed to switch to working directory '{resolvedRepoRoot}': {ex.Message}";
            return false;
        }

        repoRoot = resolvedRepoRoot;
        return true;
    }

    internal static bool TryResolveRepoRoot(string requestedWorkingDir, string launchDirectory, out string repoRoot, out string error)
    {
        repoRoot = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(requestedWorkingDir))
        {
            error = "--working-dir requires a non-empty path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(launchDirectory))
        {
            error = "Current working directory is unavailable.";
            return false;
        }

        var candidateDirectory = Path.IsPathRooted(requestedWorkingDir)
            ? Path.GetFullPath(requestedWorkingDir)
            : Path.GetFullPath(Path.Combine(launchDirectory, requestedWorkingDir));

        if (!Directory.Exists(candidateDirectory))
        {
            error = $"Working directory does not exist: {candidateDirectory}";
            return false;
        }

        if (!TryGetGitRepositoryRoot(candidateDirectory, out var gitRepoRoot, out error))
        {
            return false;
        }

        if (!Directory.Exists(gitRepoRoot))
        {
            error = $"Resolved git repository root does not exist: {gitRepoRoot}";
            return false;
        }

        repoRoot = Path.GetFullPath(gitRepoRoot);
        return true;
    }

    private static bool TryGetGitRepositoryRoot(string candidateDirectory, out string repoRoot, out string error)
    {
        try
        {
            var result = Task.Run(() => TryGetGitRepositoryRootAsync(candidateDirectory)).GetAwaiter().GetResult();
            repoRoot = result.RepoRoot;
            error = result.Error;
            return result.Success;
        }
        catch (Exception ex)
        {
            repoRoot = string.Empty;
            error = $"Failed to resolve git repository root for '{candidateDirectory}': {ex.Message}";
            return false;
        }
    }

    private static async Task<GitRepositoryRootResult> TryGetGitRepositoryRootAsync(string candidateDirectory)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(candidateDirectory);
        psi.ArgumentList.Add("rev-parse");
        psi.ArgumentList.Add("--show-toplevel");

        using var process = Process.Start(psi);
        if (process is null)
        {
            return new GitRepositoryRootResult(false, string.Empty, "Failed to start git while resolving --working-dir.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().ConfigureAwait(false);
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            details = details.Trim();
            if (string.IsNullOrWhiteSpace(details))
            {
                details = "unknown error";
            }

            return new GitRepositoryRootResult(false, string.Empty, $"Path is not inside a git repository: {candidateDirectory} ({details})");
        }

        var resolved = stdout.Trim();
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return new GitRepositoryRootResult(false, string.Empty, $"Failed to resolve git repository root for: {candidateDirectory}");
        }

        return new GitRepositoryRootResult(true, resolved, string.Empty);
    }

    private readonly record struct GitRepositoryRootResult(bool Success, string RepoRoot, string Error);
}
