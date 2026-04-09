namespace Coralph;

internal sealed record LoopIterationState(
    string IssuesJson,
    string ProgressText,
    string GeneratedTasksJson,
    string GitHead,
    string GitStatus)
{
    internal static async Task<LoopIterationState> CaptureAsync(
        LoopOptions options,
        FileContentCache fileCache,
        CancellationToken ct,
        bool invalidateArtifacts = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileCache);

        if (invalidateArtifacts)
        {
            fileCache.Invalidate(options.IssuesFile);
            fileCache.Invalidate(options.ProgressFile);
            fileCache.Invalidate(options.GeneratedTasksFile);
        }

        var issuesRead = await fileCache.TryReadTextAsync(options.IssuesFile, ct).ConfigureAwait(false);
        var progressRead = await fileCache.TryReadTextAsync(options.ProgressFile, ct).ConfigureAwait(false);
        var backlogRead = await fileCache.TryReadTextAsync(options.GeneratedTasksFile, ct).ConfigureAwait(false);

        var gitHead = await GitService.GetHeadCommitAsync(ct).ConfigureAwait(false);
        var gitStatus = await GitService.GetWorktreeStatusAsync(ct).ConfigureAwait(false);

        return new LoopIterationState(
            IssuesJson: issuesRead.Exists ? issuesRead.Content : "[]",
            ProgressText: progressRead.Exists ? progressRead.Content : string.Empty,
            GeneratedTasksJson: backlogRead.Exists ? backlogRead.Content : string.Empty,
            GitHead: gitHead,
            GitStatus: gitStatus);
    }

    internal bool HasMeaningfulChangesComparedTo(LoopIterationState previous)
    {
        return !string.Equals(IssuesJson, previous.IssuesJson, StringComparison.Ordinal) ||
               !string.Equals(ProgressText, previous.ProgressText, StringComparison.Ordinal) ||
               !string.Equals(GeneratedTasksJson, previous.GeneratedTasksJson, StringComparison.Ordinal) ||
               !string.Equals(GitHead, previous.GitHead, StringComparison.Ordinal) ||
               !string.Equals(GitStatus, previous.GitStatus, StringComparison.Ordinal);
    }

    internal bool TryGetImplicitTerminalSignal(out string signal, out string? error)
    {
        signal = string.Empty;
        error = null;

        if (!PromptHelpers.TryGetHasOpenIssues(IssuesJson, out var hasOpenIssues, out error))
        {
            return false;
        }

        if (!hasOpenIssues)
        {
            signal = TerminalSignal.NoOpenIssues;
            return true;
        }

        if (!TaskBacklog.HasOpenTasks(GeneratedTasksJson))
        {
            signal = TerminalSignal.AllTasksComplete;
            return true;
        }

        return false;
    }
}
