using System.Diagnostics;

namespace Coralph;

internal static class GhIssues
{
    internal static async Task<string> FetchOpenIssuesJsonAsync(string? repo, CancellationToken ct)
    {
        var psi = CreateFetchOpenIssuesProcessStartInfo(repo);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start `gh`");
        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);

        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"`gh` failed (exit {p.ExitCode}): {stderr}");
        }

        return stdout;
    }

    internal static ProcessStartInfo CreateFetchOpenIssuesProcessStartInfo(string? repo)
    {
        var psi = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Keep fields small + useful. `comments` is supported by gh for issue list JSON.
        psi.ArgumentList.Add("issue");
        psi.ArgumentList.Add("list");
        psi.ArgumentList.Add("--state");
        psi.ArgumentList.Add("open");
        psi.ArgumentList.Add("--limit");
        psi.ArgumentList.Add("200");
        psi.ArgumentList.Add("--json");
        psi.ArgumentList.Add("number,title,body,url,labels,comments");

        if (!string.IsNullOrWhiteSpace(repo))
        {
            psi.ArgumentList.Add("--repo");
            psi.ArgumentList.Add(repo);
        }

        return psi;
    }
}
