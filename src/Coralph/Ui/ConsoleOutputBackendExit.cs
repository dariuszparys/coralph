namespace Coralph.Ui;

internal enum ConsoleOutputBackendExitReason
{
    Completed,
    SwitchToClassic,
    StopRequested,
    UnexpectedFailure
}

internal sealed record ConsoleOutputBackendExit(
    ConsoleOutputBackendExitReason Reason,
    string? Message = null,
    Exception? Exception = null);
