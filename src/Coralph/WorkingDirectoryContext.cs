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
        repoRoot = string.Empty;
        error = string.Empty;

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

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                error = "Failed to start git while resolving --working-dir.";
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                details = details.Trim();
                if (string.IsNullOrWhiteSpace(details))
                {
                    details = "unknown error";
                }

                error = $"Path is not inside a git repository: {candidateDirectory} ({details})";
                return false;
            }

            var resolved = stdout.Trim();
            if (string.IsNullOrWhiteSpace(resolved))
            {
                error = $"Failed to resolve git repository root for: {candidateDirectory}";
                return false;
            }

            repoRoot = resolved;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to resolve git repository root for '{candidateDirectory}': {ex.Message}";
            return false;
        }
    }
}
