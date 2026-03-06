using Coralph;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Coralph.Tests;

[Collection("ConsoleOutput")]
public class ToolOutputStylingTests
{
    [Fact]
    public void ToolStart_DisplaysToolName()
    {
        var console = new TestConsole();
        ConsoleOutput.Configure(console, console);

        ConsoleOutput.WriteToolStart("sample_tool");

        var output = console.Output;
        Assert.Contains("sample_tool", output);
        ConsoleOutput.Reset();
    }

    [Fact]
    public void WriteAssistant_UsesGreenMarkup()
    {
        var console = new TestConsole();
        ConsoleOutput.Configure(console, console);

        ConsoleOutput.WriteAssistant("Hello");

        var output = console.Output;
        Assert.Contains("Hello", output);
        ConsoleOutput.Reset();
    }

    [Fact]
    public void WriteReasoning_UsesCyanMarkup()
    {
        var console = new TestConsole();
        ConsoleOutput.Configure(console, console);

        ConsoleOutput.WriteReasoning("Thinking...");

        var output = console.Output;
        Assert.Contains("Thinking...", output);
        ConsoleOutput.Reset();
    }
}
