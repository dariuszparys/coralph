using Coralph;

namespace Coralph.Tests;

public class TaskStatusTests
{
    [Theory]
    [InlineData(null, "open")]
    [InlineData("", "open")]
    [InlineData("open", "open")]
    [InlineData("done", "done")]
    [InlineData("complete", "done")]
    [InlineData("completed", "done")]
    [InlineData("in-progress", "in_progress")]
    [InlineData("in progress", "in_progress")]
    [InlineData("inprogress", "in_progress")]
    [InlineData("blocked", "blocked")]
    [InlineData("unknown", "open")]
    public void NormalizeStatus_MapsExpectedValues(string? input, string expected)
    {
        var normalized = TaskStatusHelper.NormalizeStatus(input);

        Assert.Equal(expected, normalized);
    }
}
