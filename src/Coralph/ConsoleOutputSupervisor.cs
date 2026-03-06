using Coralph.Ui;
using Serilog;

namespace Coralph;

internal static class ConsoleOutputSupervisor
{
    internal static Task? Start(CancellationTokenSource runCancellation, CancellationToken monitorCancellation)
    {
        ArgumentNullException.ThrowIfNull(runCancellation);

        var exitTask = ConsoleOutput.GetBackendExitTask();
        if (exitTask is null)
        {
            return null;
        }

        return MonitorAsync(exitTask, runCancellation, monitorCancellation);
    }

    internal static async Task ApplyBackendExitAsync(
        ConsoleOutputBackendExit exit,
        CancellationTokenSource runCancellation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(runCancellation);

        switch (exit.Reason)
        {
            case ConsoleOutputBackendExitReason.Completed:
                return;
            case ConsoleOutputBackendExitReason.StopRequested:
                if (!runCancellation.IsCancellationRequested)
                {
                    runCancellation.Cancel();
                }
                return;
            case ConsoleOutputBackendExitReason.SwitchToClassic:
                try
                {
                    await SwitchToClassicAsync(
                        warning: exit.Message ?? "Leaving TUI. Continuing in classic output.",
                        ct).ConfigureAwait(false);
                }
                catch (Exception fallbackEx)
                {
                    Log.Error(fallbackEx, "Failed to switch from TUI to classic output");
                    if (!runCancellation.IsCancellationRequested)
                    {
                        runCancellation.Cancel();
                    }

                    Console.Error.WriteLine("Coralph failed to switch from TUI to classic output and is stopping.");
                }
                return;
            case ConsoleOutputBackendExitReason.UnexpectedFailure:
                if (exit.Exception is not null)
                {
                    Log.Warning(exit.Exception, "TUI exited unexpectedly");
                }
                else
                {
                    Log.Warning("TUI exited unexpectedly: {Message}", exit.Message ?? "(no details)");
                }

                try
                {
                    await SwitchToClassicAsync(
                        warning: exit.Message is null
                            ? "TUI stopped unexpectedly. Continuing in classic output."
                            : $"TUI stopped unexpectedly: {exit.Message}. Continuing in classic output.",
                        ct).ConfigureAwait(false);
                }
                catch (Exception fallbackEx)
                {
                    Log.Error(fallbackEx, "Failed to fall back to classic console output after TUI failure");
                    if (!runCancellation.IsCancellationRequested)
                    {
                        runCancellation.Cancel();
                    }

                    Console.Error.WriteLine("TUI failed and fallback to classic output also failed. Coralph is stopping.");
                }
                return;
            default:
                return;
        }
    }

    private static async Task MonitorAsync(
        Task<ConsoleOutputBackendExit> exitTask,
        CancellationTokenSource runCancellation,
        CancellationToken monitorCancellation)
    {
        ConsoleOutputBackendExit exit;
        try
        {
            exit = await exitTask.WaitAsync(monitorCancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (monitorCancellation.IsCancellationRequested)
        {
            return;
        }

        await ApplyBackendExitAsync(exit, runCancellation, monitorCancellation).ConfigureAwait(false);
    }

    private static async Task SwitchToClassicAsync(string warning, CancellationToken ct)
    {
        if (!ConsoleOutput.UsesTui)
        {
            return;
        }

        await ConsoleOutput.SwitchToClassicAsync(ct).ConfigureAwait(false);
        ConsoleOutput.WriteWarningLine(warning);
    }
}
