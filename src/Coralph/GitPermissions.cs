using System.Diagnostics;
using System.Text.Json;
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
        if (string.IsNullOrWhiteSpace(url))
            return (null, null);

        // Handle HTTPS: https://github.com/owner/repo.git or https://github.com/owner/repo
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                var owner = segments[0];
                var repo = segments[1];
                if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    repo = repo[..^4];

                return (owner, repo);
            }
        }

        // Handle SSH: git@github.com:owner/repo.git or git@github.com:owner/repo
        var sshMatch = Regex.Match(url, @"^git@github\.com:(?<owner>[^/]+)/(?<repo>.+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);
        if (sshMatch.Success)
        {
            return (sshMatch.Groups["owner"].Value, sshMatch.Groups["repo"].Value);
        }

        return (null, null);
    }

    internal static async Task<bool> CanPushToMainAsync(string owner, string repo, CancellationToken ct)
    {
        try
        {
            var repoJson = await RunGhApiAsync($"repos/{owner}/{repo}", ct);
            if (string.IsNullOrWhiteSpace(repoJson))
            {
                return false;
            }

            using var doc = JsonDocument.Parse(repoJson);
            var root = doc.RootElement;

            var canPush = root.TryGetProperty("permissions", out var permissions) &&
                          permissions.TryGetProperty("push", out var pushProp) &&
                          pushProp.ValueKind == JsonValueKind.True;
            if (!canPush)
            {
                return false;
            }

            var defaultBranch = root.TryGetProperty("default_branch", out var branchProp) &&
                                branchProp.ValueKind == JsonValueKind.String
                ? branchProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(defaultBranch))
            {
                return false;
            }

            var escapedBranch = Uri.EscapeDataString(defaultBranch);
            var protectionResult = await RunGhApiAsync($"repos/{owner}/{repo}/branches/{escapedBranch}", ct, "--jq", ".protected");
            var protectionValue = protectionResult.Trim();
            if (string.Equals(protectionValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(protectionValue, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
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
