using Coralph;

namespace Coralph.Tests;

public class TerminalSignalTests
{
    [Fact]
    public void All_ContainsExpectedSignals()
    {
        Assert.Contains(TerminalSignal.Complete, TerminalSignal.All);
        Assert.Contains(TerminalSignal.AllTasksComplete, TerminalSignal.All);
        Assert.Contains(TerminalSignal.NoOpenIssues, TerminalSignal.All);
    }

    [Fact]
    public void All_IsCaseInsensitive()
    {
        Assert.Contains("complete", TerminalSignal.All);
        Assert.Contains("all_tasks_complete", TerminalSignal.All);
        Assert.Contains("no_open_issues", TerminalSignal.All);
    }
}
