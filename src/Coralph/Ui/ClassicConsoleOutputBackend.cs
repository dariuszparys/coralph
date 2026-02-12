using Spectre.Console;

namespace Coralph.Ui;

internal sealed class ClassicConsoleOutputBackend : IConsoleOutputBackend
{
    public bool UsesTui => false;

    public IAnsiConsole Out
    {
        get => _out ??= ConsoleOutput.CreateConsole(Console.Out, Console.IsOutputRedirected);
        private set => _out = value;
    }

    public IAnsiConsole Error
    {
        get => _error ??= ConsoleOutput.CreateConsole(Console.Error, Console.IsErrorRedirected);
        private set => _error = value;
    }

    private IAnsiConsole? _out;
    private IAnsiConsole? _error;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Configure(IAnsiConsole? stdout, IAnsiConsole? stderr)
    {
        Out = stdout ?? ConsoleOutput.CreateConsole(Console.Out, Console.IsOutputRedirected);
        Error = stderr ?? ConsoleOutput.CreateConsole(Console.Error, Console.IsErrorRedirected);
    }

    public void Reset()
    {
        _out = null;
        _error = null;
    }

    public void Write(string text) => Out.Write(text);

    public void WriteLine() => Out.WriteLine();

    public void WriteLine(string text) => Out.WriteLine(text);

    public void WriteError(string text) => Error.Write(text);

    public void WriteErrorLine() => Error.WriteLine();

    public void WriteErrorLine(string text) => Error.WriteLine(text);

    public void WriteWarningLine(string text)
    {
        if (Console.IsErrorRedirected)
        {
            Error.WriteLine(text);
        }
        else
        {
            Error.MarkupLine($"[yellow]{Markup.Escape(text)}[/]");
        }
    }

    public void MarkupLine(string markup) => Out.MarkupLine(markup);

    public void MarkupLineInterpolated(FormattableString markup) => Out.MarkupLineInterpolated(markup);

    public void WriteReasoning(string text)
    {
        if (Console.IsOutputRedirected)
        {
            Write(text);
        }
        else
        {
            Out.Markup($"[dim cyan]{Markup.Escape(text)}[/]");
        }
    }

    public void WriteAssistant(string text)
    {
        if (Console.IsOutputRedirected)
        {
            Write(text);
        }
        else
        {
            Out.Markup($"[green]{Markup.Escape(text)}[/]");
        }
    }

    public void WriteToolStart(string toolName)
    {
        if (Console.IsOutputRedirected)
        {
            WriteLine($"[Tool: {toolName}]");
        }
        else
        {
            MarkupLineInterpolated($"[black on yellow] ▶ {toolName} [/]");
        }
    }

    public void WriteToolComplete(string toolName, string summary)
    {
        if (Console.IsOutputRedirected)
        {
            WriteLine(summary);
        }
        else
        {
            Out.MarkupLine($"[dim yellow]{Markup.Escape(summary)}[/]");
        }
    }

    public void WriteSectionSeparator(string title)
    {
        if (Console.IsOutputRedirected)
        {
            WriteLine($"\n--- {title} ---\n");
        }
        else
        {
            WriteLine();
            Out.Write(new Rule($"[bold blue]{title}[/]") { Justification = Justify.Left });
            WriteLine();
        }
    }

    public void RefreshGeneratedTasks()
    {
        // No-op for classic output.
    }

    public Task<int> PromptSelectionAsync(
        string title,
        IReadOnlyList<string> options,
        int defaultIndex,
        CancellationToken ct = default)
    {
        if (options is null || options.Count == 0)
        {
            return Task.FromResult(defaultIndex);
        }

        WriteLine(title);
        WriteLine("Select your project type:");

        for (var i = 0; i < options.Count; i++)
        {
            WriteLine($"  {i + 1}) {options[i]}");
        }

        Write($"Enter number (1-{options.Count}): ");

        var choice = Console.ReadLine()?.Trim();
        if (!int.TryParse(choice, out var parsed))
        {
            return Task.FromResult(defaultIndex);
        }

        var selectedIndex = parsed - 1;
        if (selectedIndex < 0 || selectedIndex >= options.Count)
        {
            return Task.FromResult(defaultIndex);
        }

        return Task.FromResult(selectedIndex);
    }
}
