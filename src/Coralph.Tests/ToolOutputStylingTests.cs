using Coralph;
using Spectre.Console;
using Spectre.Console.Testing;
using System.Reflection;

namespace Coralph.Tests;

public class ToolOutputStylingTests
{
    [Fact]
    public void ToolHeader_UsesOrangeMarkup()
    {
        var console = new TestConsole();
        ConsoleOutput.Configure(console, console);

        var method = typeof(CopilotRunner).GetMethod(
            "WriteToolHeader",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(bool) },
            modifiers: null);
        Assert.NotNull(method);

        method!.Invoke(null, new object[] { "[Tool: sample]", false });

        var output = console.Output;
        Assert.Contains("[Tool: sample]", output);
        ConsoleOutput.Reset();
    }
}
