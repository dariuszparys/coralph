namespace Coralph;

internal static class TerminalSignal
{
    internal const string Complete = "COMPLETE";
    internal const string AllTasksComplete = "ALL_TASKS_COMPLETE";
    internal const string NoOpenIssues = "NO_OPEN_ISSUES";

    internal static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Complete,
        AllTasksComplete,
        NoOpenIssues
    };
}
