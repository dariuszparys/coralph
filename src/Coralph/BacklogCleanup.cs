namespace Coralph;

internal static class BacklogCleanup
{
    internal static bool ShouldDeleteForTerminalSignal(string terminalSignal)
    {
        return string.Equals(terminalSignal, "COMPLETE", StringComparison.OrdinalIgnoreCase)
               || string.Equals(terminalSignal, "ALL_TASKS_COMPLETE", StringComparison.OrdinalIgnoreCase)
               || string.Equals(terminalSignal, "NO_OPEN_ISSUES", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryDelete(string? backlogFile, out Exception? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(backlogFile) || !File.Exists(backlogFile))
        {
            return false;
        }

        try
        {
            File.Delete(backlogFile);
            FileContentCache.Shared.Invalidate(backlogFile);
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }
}
