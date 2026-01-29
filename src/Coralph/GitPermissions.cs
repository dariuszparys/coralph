using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Coralph;

internal static class GitPermissions
{
    internal static async Task<(string? Owner, string? Repo)> GetRepoFromGitRemoteAsync(CancellationToken ct)
    {
        var remoteUrl = await RunGitAsync("remote get-url origin", ct);
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return (null, null);

        return ParseGitHubUrl(remoteUrl);
    }

    internal static (string? Owner, string? Repo) ParseGitHubUrl(string url)
    {
        // Handle HTTPS: https://github.com/owner/repo.git or https://github.com/owner/repo
        var httpsMatch = Regex.Match(url, @"https://github\.com/([^/]+)/([^/\.]+)");
        if (httpsMatch.Success)
            return (httpsMatch.Groups[1].Value, httpsMatch.Groups[2].Value);

        // Handle SSH: git@github.com:owner/repo.git or git@github.com:owner/repo
        var sshMatch = Regex.Match(url, @"git@github\.com:([^/]+)/([^/\.]+)");
        if (sshMatch.Success)
            return (sshMatch.Groups[1].Value, sshMatch.Groups[2].Value);

        return (null, null);
    }

    internal static async Task<bool> CanPushToMainAsync(string owner, string repo, CancellationToken ct)
    {
        try
        {
            var result = await RunGhApiAsync($"repos/{owner}/{repo}", ct, "--jq", ".permissions.push");
            return string.Equals(result.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If API call fails (auth issues, rate limit, etc.), default to PR mode (safer)
            return false;
        }
    }

    internal static async Task<string?> GetCurrentUserLoginAsync(CancellationToken ct)
    {
        try
        {
            var result = await RunGhApiAsync("user", ct, "--jq", ".login");
            var login = result.Trim();
            return string.IsNullOrWhiteSpace(login) ? null : login;
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsUserInBypassList(string? login, IEnumerable<string>? bypassUsers)
    {
        if (string.IsNullOrWhiteSpace(login) || bypassUsers is null)
            return false;

        foreach (var user in bypassUsers)
        {
            if (string.IsNullOrWhiteSpace(user))
                continue;

            if (string.Equals(user.Trim(), login, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task<string> RunGitAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            return string.Empty;

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output.Trim();
    }

    private static async Task<string> RunGhApiAsync(string endpoint, CancellationToken ct, params string[] args)
    {
        var allArgs = new List<string> { "api", endpoint };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("gh", string.Join(" ", allArgs))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            return string.Empty;

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"gh api failed with exit code {process.ExitCode}");

        return output;
    }
}
