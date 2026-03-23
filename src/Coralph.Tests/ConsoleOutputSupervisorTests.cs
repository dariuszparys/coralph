using Coralph;
using Coralph.Ui;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Coralph.Tests;

[Collection("ConsoleOutput")]
public class ConsoleOutputSupervisorTests
{
    [Fact]
    public async Task ApplyBackendExitAsync_UnexpectedFailure_FallsBackToClassic()
    {
        var backend = new FakeTuiBackend();
        await ConsoleOutput.UseBackendAsync(backend);

        using var cts = new CancellationTokenSource();

        await ConsoleOutputSupervisor.ApplyBackendExitAsync(
            new ConsoleOutputBackendExit(ConsoleOutputBackendExitReason.UnexpectedFailure, "boom"),
            cts,
            CancellationToken.None);

        Assert.False(ConsoleOutput.UsesTui);
        Assert.True(backend.Disposed);

        await ConsoleOutput.ResetAsync();
    }

    [Fact]
    public async Task ApplyBackendExitAsync_StopRequested_CancelsRun()
    {
        using var cts = new CancellationTokenSource();

        await ConsoleOutputSupervisor.ApplyBackendExitAsync(
            new ConsoleOutputBackendExit(ConsoleOutputBackendExitReason.StopRequested, "stop"),
            cts,
            CancellationToken.None);

        Assert.True(cts.IsCancellationRequested);
        await ConsoleOutput.ResetAsync();
    }

    private sealed class FakeTuiBackend : IConsoleOutputBackend
    {
        private readonly TestConsole _console = new();

        public bool UsesTui => true;
        public IAnsiConsole Out => _console;
        public IAnsiConsole Error => _console;
        public Task<ConsoleOutputBackendExit>? ExitTask => null;
        public bool Disposed { get; private set; }

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        public void Configure(IAnsiConsole? stdout, IAnsiConsole? stderr)
        {
        }

        public void Reset()
        {
        }

        public void Write(string text)
        {
        }

        public void WriteLine()
        {
        }

        public void WriteLine(string text)
        {
        }

        public void WriteError(string text)
        {
        }

        public void WriteErrorLine()
        {
        }

        public void WriteErrorLine(string text)
        {
        }

        public void WriteWarningLine(string text)
        {
        }

        public void MarkupLine(string markup)
        {
        }

        public void MarkupLineInterpolated(FormattableString markup)
        {
        }

        public void WriteReasoning(string text)
        {
        }

        public void WriteAssistant(string text)
        {
        }

        public void WriteToolStart(string toolName)
        {
        }

        public void WriteToolComplete(string toolName, string summary)
        {
        }

        public void WriteSectionSeparator(string title)
        {
        }

        public void RefreshGeneratedTasks()
        {
        }

        public Task<int> PromptSelectionAsync(string title, IReadOnlyList<string> options, int defaultIndex, CancellationToken ct = default)
        {
            return Task.FromResult(defaultIndex);
        }
    }
}
