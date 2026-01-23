using System.Diagnostics;

namespace Coralph;

internal static class GhIssues
{
    internal static async Task<string> FetchOpenIssuesJsonAsync(string? repo, CancellationToken ct)
    {
        // Keep fields small + useful. `comments` is supported by gh for issue list JSON.
        var args = "issue list --state open --limit 200 --json number,title,body,url,labels,comments";
        if (!string.IsNullOrWhiteSpace(repo))
        {
            args += $" --repo {repo}";
        }

        var psi = new ProcessStartInfo("gh", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start `gh`");
        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);

        await p.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"`gh` failed (exit {p.ExitCode}): {stderr}");
        }

        return stdout;
    }
}
