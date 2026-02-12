using Spectre.Console;

namespace Coralph.Ui;

internal interface IConsoleOutputBackend : IProjectTypePrompt, IAsyncDisposable
{
    bool UsesTui { get; }
    IAnsiConsole Out { get; }
    IAnsiConsole Error { get; }

    Task InitializeAsync(CancellationToken ct = default);

    void Configure(IAnsiConsole? stdout, IAnsiConsole? stderr);
    void Reset();

    void Write(string text);
    void WriteLine();
    void WriteLine(string text);

    void WriteError(string text);
    void WriteErrorLine();
    void WriteErrorLine(string text);

    void WriteWarningLine(string text);
    void MarkupLine(string markup);
    void MarkupLineInterpolated(FormattableString markup);

    void WriteReasoning(string text);
    void WriteAssistant(string text);
    void WriteToolStart(string toolName);
    void WriteToolComplete(string toolName, string summary);
    void WriteSectionSeparator(string title);

    void RefreshGeneratedTasks();
}
