using System.Diagnostics;
using System.Text.Json;

namespace Coralph;

internal record PrFeedbackComment(
    string Type,
    string Author,
    string Body,
    string? Path,
    int? Line,
    bool IsResolved);

internal record PrFeedbackData(
    int IssueNumber,
    int PrNumber,
    string PrBranch,
    List<PrFeedbackComment> Feedback);

internal static class PrFeedback
{
    private const string ReviewThreadsQuery =
        "query($owner:String!, $repo:String!, $prNumber:Int!) {" +
        " repository(owner:$owner, name:$repo) {" +
        "  pullRequest(number:$prNumber) {" +
        "   reviewThreads(first:100) {" +
        "    nodes {" +
        "     isResolved " +
        "     comments(first:50) { nodes { author { login } body path line } }" +
        "    }" +
        "   }" +
        "  }" +
        " }" +
        "}";

    internal static async Task<List<int>> FindOpenPrsForIssueAsync(int issueNumber, string owner, string repo, CancellationToken ct)
    {
        try
        {
            // Search for PRs that reference this issue
            var searchQuery = $"repo:{owner}/{repo} is:pr is:open {issueNumber} in:body,title";
            var result = await RunGhAsync($"pr list --search \"{searchQuery}\" --json number --jq '.[].number'", ct);

            if (string.IsNullOrWhiteSpace(result))
                return new List<int>();

            return result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToList();
        }
        catch
        {
            return new List<int>();
        }
    }

    internal static async Task<PrFeedbackData?> GetPrFeedbackAsync(int issueNumber, int prNumber, string owner, string repo, CancellationToken ct)
    {
        try
        {
            // Get PR details including branch name
            var prDataJson = await RunGhAsync($"pr view {prNumber} --json headRefName,reviews,comments", ct);
            if (string.IsNullOrWhiteSpace(prDataJson))
                return null;

            using var prDoc = JsonDocument.Parse(prDataJson);
            var root = prDoc.RootElement;

            var branchName = root.GetProperty("headRefName").GetString() ?? $"coralph/issue-{issueNumber}";
            var feedback = new List<PrFeedbackComment>();

            // Process review comments (threads on specific code lines)
            if (root.TryGetProperty("reviews", out var reviews))
            {
                foreach (var review in reviews.EnumerateArray())
                {
                    if (!review.TryGetProperty("body", out var bodyProp) || bodyProp.ValueKind != JsonValueKind.String)
                        continue;

                    var body = bodyProp.GetString() ?? "";
                    var author = review.TryGetProperty("author", out var authorProp) &&
                                authorProp.TryGetProperty("login", out var loginProp)
                        ? loginProp.GetString() ?? "unknown"
                        : "unknown";

                    var state = review.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : null;

                    // Check for @coralph mentions
                    if (body.Contains("@coralph", StringComparison.OrdinalIgnoreCase))
                    {
                        feedback.Add(new PrFeedbackComment(
                            Type: "mention",
                            Author: author,
                            Body: body,
                            Path: null,
                            Line: null,
                            IsResolved: string.Equals(state, "DISMISSED", StringComparison.OrdinalIgnoreCase)
                        ));
                    }
                }
            }

            // Process general PR comments
            if (root.TryGetProperty("comments", out var comments))
            {
                foreach (var comment in comments.EnumerateArray())
                {
                    if (!comment.TryGetProperty("body", out var bodyProp) || bodyProp.ValueKind != JsonValueKind.String)
                        continue;

                    var body = bodyProp.GetString() ?? "";
                    var author = comment.TryGetProperty("author", out var authorProp) &&
                                authorProp.TryGetProperty("login", out var loginProp)
                        ? loginProp.GetString() ?? "unknown"
                        : "unknown";

                    // Only include comments with @coralph mentions
                    if (body.Contains("@coralph", StringComparison.OrdinalIgnoreCase))
                    {
                        feedback.Add(new PrFeedbackComment(
                            Type: "mention",
                            Author: author,
                            Body: body,
                            Path: null,
                            Line: null,
                            IsResolved: false
                        ));
                    }
                }
            }

            // Fetch review threads (GraphQL exposes isResolved, REST does not)
            var threadsJson = await RunGhApiAsync(
                [
                    "graphql",
                    "-f", $"query={ReviewThreadsQuery}",
                    "-f", $"owner={owner}",
                    "-f", $"repo={repo}",
                    "-F", $"prNumber={prNumber}"
                ],
                ct);
            if (!string.IsNullOrWhiteSpace(threadsJson))
            {
                using var threadsDoc = JsonDocument.Parse(threadsJson);
                if (threadsDoc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("repository", out var repository) &&
                    repository.TryGetProperty("pullRequest", out var pullRequest) &&
                    pullRequest.TryGetProperty("reviewThreads", out var reviewThreads) &&
                    reviewThreads.TryGetProperty("nodes", out var threadNodes) &&
                    threadNodes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var thread in threadNodes.EnumerateArray())
                    {
                        var isResolved = thread.TryGetProperty("isResolved", out var resolvedProp) &&
                                         resolvedProp.ValueKind == JsonValueKind.True;

                        if (!thread.TryGetProperty("comments", out var threadComments) ||
                            !threadComments.TryGetProperty("nodes", out var commentNodes) ||
                            commentNodes.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        PrFeedbackComment? firstComment = null;
                        var addedMention = false;

                        foreach (var comment in commentNodes.EnumerateArray())
                        {
                            var body = comment.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String
                                ? bodyProp.GetString()
                                : null;
                            if (string.IsNullOrWhiteSpace(body))
                            {
                                continue;
                            }

                            var author = comment.TryGetProperty("author", out var authorProp) &&
                                         authorProp.TryGetProperty("login", out var loginProp) &&
                                         loginProp.ValueKind == JsonValueKind.String
                                ? loginProp.GetString() ?? "unknown"
                                : "unknown";

                            var path = comment.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String
                                ? pathProp.GetString()
                                : null;

                            int? line = null;
                            if (comment.TryGetProperty("line", out var lineProp) &&
                                lineProp.ValueKind == JsonValueKind.Number &&
                                lineProp.TryGetInt32(out var parsedLine))
                            {
                                line = parsedLine;
                            }

                            firstComment ??= new PrFeedbackComment(
                                Type: "unresolved_thread",
                                Author: author,
                                Body: body,
                                Path: path,
                                Line: line,
                                IsResolved: isResolved);

                            if (body.Contains("@coralph", StringComparison.OrdinalIgnoreCase))
                            {
                                feedback.Add(new PrFeedbackComment(
                                    Type: "mention",
                                    Author: author,
                                    Body: body,
                                    Path: path,
                                    Line: line,
                                    IsResolved: isResolved
                                ));
                                addedMention = true;
                            }
                        }

                        if (!isResolved && !addedMention && firstComment is not null)
                        {
                            feedback.Add(firstComment);
                        }
                    }
                }
            }

            if (feedback.Count == 0)
                return null;

            return new PrFeedbackData(issueNumber, prNumber, branchName, feedback);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> RunGhAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("gh", arguments)
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

    private static async Task<string> RunGhApiAsync(string endpoint, CancellationToken ct)
    {
        return await RunGhApiAsync([endpoint], ct);
    }

    private static async Task<string> RunGhApiAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("api");
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process is null)
            return string.Empty;

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            return string.Empty;

        return output.Trim();
    }
}
