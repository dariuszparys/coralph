using System.Text;
using Coralph.Ui;
using Coralph.Ui.Tui;
using Spectre.Console;

namespace Coralph;

internal static class ConsoleOutput
{
    private static IConsoleOutputBackend _backend = new ClassicConsoleOutputBackend();
    private static Action? _stopRequested;

    internal static bool UsesTui => _backend.UsesTui;

    internal static IAnsiConsole Out => _backend.Out;

    internal static IAnsiConsole Error => _backend.Error;

    internal static TextWriter OutWriter { get; } = new ConsoleOutputWriter(isError: false);
    internal static TextWriter ErrorWriter { get; } = new ConsoleOutputWriter(isError: true);

    internal static async Task ConfigureForModeAsync(UiMode mode, LoopOptions options, CancellationToken ct = default)
    {
        IConsoleOutputBackend backend = mode == UiMode.Tui
            ? new Hex1bConsoleOutputBackend(options)
            : new ClassicConsoleOutputBackend();

        await UseBackendAsync(backend, ct).ConfigureAwait(false);
    }

    internal static async Task UseBackendAsync(IConsoleOutputBackend backend, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(backend);

        var previous = Interlocked.Exchange(ref _backend, backend);

        try
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort: old backend might already be shutting down.
        }

        await _backend.InitializeAsync(ct).ConfigureAwait(false);
    }

    internal static async Task DisposeBackendAsync()
    {
        var backend = Interlocked.Exchange(ref _backend, new ClassicConsoleOutputBackend());
        await backend.DisposeAsync().ConfigureAwait(false);
    }

    internal static IDisposable PushStopRequestHandler(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var previous = Interlocked.Exchange(ref _stopRequested, handler);
        return new StopRequestHandlerScope(previous);
    }

    internal static void RequestStop()
    {
        var handler = Interlocked.CompareExchange(ref _stopRequested, null, null);
        if (handler is null)
        {
            return;
        }

        try
        {
            handler();
        }
        catch
        {
            // Best effort: stop requests should not crash output pipeline.
        }
    }

    internal static void Configure(IAnsiConsole? stdout, IAnsiConsole? stderr) => _backend.Configure(stdout, stderr);

    internal static void Reset()
    {
        var backend = Interlocked.Exchange(ref _backend, new ClassicConsoleOutputBackend());
        try
        {
            backend.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort: tests should still proceed.
        }
    }

    internal static void Write(string text) => _backend.Write(text);

    internal static void WriteLine() => _backend.WriteLine();

    internal static void WriteLine(string text) => _backend.WriteLine(text);

    internal static void WriteError(string text) => _backend.WriteError(text);

    internal static void WriteErrorLine() => _backend.WriteErrorLine();

    internal static void WriteErrorLine(string text) => _backend.WriteErrorLine(text);

    internal static void WriteWarningLine(string text) => _backend.WriteWarningLine(text);

    internal static void MarkupLine(string markup) => _backend.MarkupLine(markup);

    internal static void MarkupLineInterpolated(FormattableString markup) => _backend.MarkupLineInterpolated(markup);

    internal static void WriteReasoning(string text) => _backend.WriteReasoning(text);

    internal static void WriteAssistant(string text) => _backend.WriteAssistant(text);

    internal static void WriteToolStart(string toolName) => _backend.WriteToolStart(toolName);

    internal static void WriteToolComplete(string toolName, string summary) => _backend.WriteToolComplete(toolName, summary);

    internal static void WriteSectionSeparator(string title) => _backend.WriteSectionSeparator(title);

    internal static void RefreshGeneratedTasks() => _backend.RefreshGeneratedTasks();

    internal static Task<int> PromptSelectionAsync(
        string title,
        IReadOnlyList<string> options,
        int defaultIndex,
        CancellationToken ct = default)
    {
        return _backend.PromptSelectionAsync(title, options, defaultIndex, ct);
    }

    internal static IAnsiConsole CreateConsole(TextWriter writer, bool isRedirected)
    {
        var settings = new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = isRedirected ? AnsiSupport.No : AnsiSupport.Detect,
            Interactive = isRedirected ? InteractionSupport.No : InteractionSupport.Detect,
        };

        return AnsiConsole.Create(settings);
    }

    private sealed class ConsoleOutputWriter(bool isError) : TextWriter
    {
        private readonly bool _isError = isError;

        public override Encoding Encoding => Console.OutputEncoding;

        public override void Write(char value) => Write(value.ToString());

        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer is null || count <= 0)
            {
                return;
            }

            Write(new string(buffer, index, count));
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (_isError)
            {
                ConsoleOutput.WriteError(value);
            }
            else
            {
                ConsoleOutput.Write(value);
            }
        }

        public override void WriteLine()
        {
            if (_isError)
            {
                ConsoleOutput.WriteErrorLine();
            }
            else
            {
                ConsoleOutput.WriteLine();
            }
        }

        public override void WriteLine(string? value)
        {
            if (value is null)
            {
                if (_isError)
                {
                    ConsoleOutput.WriteErrorLine();
                }
                else
                {
                    ConsoleOutput.WriteLine();
                }

                return;
            }

            if (_isError)
            {
                ConsoleOutput.WriteErrorLine(value);
            }
            else
            {
                ConsoleOutput.WriteLine(value);
            }
        }
    }

    private sealed class StopRequestHandlerScope(Action? previous) : IDisposable
    {
        private Action? _previous = previous;

        public void Dispose()
        {
            Interlocked.Exchange(ref _stopRequested, _previous);
            _previous = null;
        }
    }
}
