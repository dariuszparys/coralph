using System.Text;
using Spectre.Console;

namespace Coralph;

internal static class ConsoleOutput
{
    private static IAnsiConsole? _outConsole;
    private static IAnsiConsole? _errorConsole;

    internal static IAnsiConsole Out => _outConsole ??= CreateConsole(Console.Out, Console.IsOutputRedirected);
    internal static IAnsiConsole Error => _errorConsole ??= CreateConsole(Console.Error, Console.IsErrorRedirected);

    internal static TextWriter OutWriter { get; } = new ConsoleOutputWriter(isError: false);
    internal static TextWriter ErrorWriter { get; } = new ConsoleOutputWriter(isError: true);

    internal static void Configure(IAnsiConsole? stdout, IAnsiConsole? stderr)
    {
        _outConsole = stdout;
        _errorConsole = stderr;
    }

    internal static void Reset()
    {
        _outConsole = null;
        _errorConsole = null;
    }

    internal static void Write(string text) => Out.Write(text);

    internal static void WriteLine() => Out.WriteLine();

    internal static void WriteLine(string text) => Out.WriteLine(text);

    internal static void WriteError(string text) => Error.Write(text);

    internal static void WriteErrorLine() => Error.WriteLine();

    internal static void WriteErrorLine(string text) => Error.WriteLine(text);

    internal static void MarkupLine(string markup) => Out.MarkupLine(markup);

    private static IAnsiConsole CreateConsole(TextWriter writer, bool isRedirected)
    {
        var settings = new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = isRedirected ? AnsiSupport.No : AnsiSupport.Detect,
            Interactive = isRedirected ? InteractionSupport.No : InteractionSupport.Detect,
        };

        return AnsiConsole.Create(settings);
    }

    private sealed class ConsoleOutputWriter : TextWriter
    {
        private readonly bool _isError;

        internal ConsoleOutputWriter(bool isError)
        {
            _isError = isError;
        }

        public override Encoding Encoding => Console.OutputEncoding;

        public override void Write(char value) => Write(value.ToString());

        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer is null || count <= 0) return;
            Write(new string(buffer, index, count));
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (_isError) ConsoleOutput.WriteError(value);
            else ConsoleOutput.Write(value);
        }

        public override void WriteLine()
        {
            if (_isError) ConsoleOutput.WriteErrorLine();
            else ConsoleOutput.WriteLine();
        }

        public override void WriteLine(string? value)
        {
            if (value is null)
            {
                if (_isError) ConsoleOutput.WriteErrorLine();
                else ConsoleOutput.WriteLine();
                return;
            }

            if (_isError) ConsoleOutput.WriteErrorLine(value);
            else ConsoleOutput.WriteLine(value);
        }
    }
}
