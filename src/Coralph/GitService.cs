using System.Diagnostics;
using Serilog;

namespace Coralph;

internal static class GitService
{
    internal static async Task CommitProgressIfNeededAsync(string progressFile, CancellationToken ct)
    {
        if (!File.Exists(progressFile))
        {
            return;
        }

        var statusResult = await RunGitAsync(["status", "--porcelain", "--", progressFile], ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(statusResult))
        {
            return;
        }

        await RunGitAsync(["add", progressFile], ct).ConfigureAwait(false);
        var commitResult = await RunGitAsync(["commit", "-m", "chore: update progress.txt"], ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(commitResult))
        {
            ConsoleOutput.WriteLine($"Auto-committed {progressFile}");
        }
    }

    internal static async Task<string> RunGitAsync(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            Log.Warning("Failed to start git for arguments: {Arguments}", string.Join(' ', arguments));
            return string.Empty;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var output = await stdoutTask.ConfigureAwait(false);
        var error = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var trimmedError = error?.Trim();
            Log.Warning(
                "git {Arguments} failed with exit code {ExitCode}: {Error}",
                string.Join(' ', arguments),
                process.ExitCode,
                string.IsNullOrWhiteSpace(trimmedError) ? "(no error output)" : trimmedError);
            return string.Empty;
        }

        return output.Trim();
    }
}
