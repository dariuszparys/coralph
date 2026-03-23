namespace Coralph;

internal static class BacklogCleanup
{
    internal static bool ShouldDeleteForTerminalSignal(string terminalSignal)
    {
        return TerminalSignal.All.Contains(terminalSignal);
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
