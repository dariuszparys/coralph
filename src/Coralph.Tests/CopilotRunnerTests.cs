using Coralph;

namespace Coralph.Tests;

public class CopilotRunnerTests
{
    #region SummarizeToolOutput Tests

    [Fact]
    public void SummarizeToolOutput_WithShortOutput_ReturnsAsIs()
    {
        var output = "Line 1\nLine 2\nLine 3";

        var result = CopilotRunner.SummarizeToolOutput(output);

        Assert.Contains("Line 1", result);
        Assert.Contains("Line 2", result);
        Assert.Contains("Line 3", result);
    }

    [Fact]
    public void SummarizeToolOutput_WithLongOutput_TruncatesWithSummary()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"Line {i}");
        var output = string.Join("\n", lines);

        var result = CopilotRunner.SummarizeToolOutput(output);

        Assert.Contains("Line 1", result);
        Assert.Contains("lines", result);
        Assert.Contains("chars", result);
    }

    [Fact]
    public void SummarizeToolOutput_WithEmptyString_ReturnsEmpty()
    {
        var result = CopilotRunner.SummarizeToolOutput("");

        Assert.Empty(result);
    }

    [Fact]
    public void SummarizeToolOutput_WithWhitespace_ReturnsEmpty()
    {
        var result = CopilotRunner.SummarizeToolOutput("   \n  \n  ");

        Assert.Empty(result);
    }

    [Fact]
    public void SummarizeToolOutput_NormalizesLineEndings()
    {
        var output = "Line 1\r\nLine 2\r\nLine 3";

        var result = CopilotRunner.SummarizeToolOutput(output);

        Assert.Contains("Line 1", result);
        Assert.Contains("Line 2", result);
    }

    [Fact]
    public void SummarizeToolOutput_LimitsToMaxLines()
    {
        var lines = Enumerable.Range(1, 10).Select(i => $"Line {i}");
        var output = string.Join("\n", lines);

        var result = CopilotRunner.SummarizeToolOutput(output);

        // Should show first 6 lines max plus summary
        Assert.Contains("Line 1", result);
        Assert.Contains("Line 6", result);
    }

    [Fact]
    public void SummarizeToolOutput_WithVeryLongLine_Truncates()
    {
        var longLine = new string('x', 1000);
        var output = longLine;

        var result = CopilotRunner.SummarizeToolOutput(output);

        Assert.Contains("truncated", result);
        Assert.True(result.Length < output.Length);
    }

    #endregion

    #region IsIgnorableToolOutput Tests

    [Fact]
    public void IsIgnorableToolOutput_WithReportIntent_ReturnsTrue()
    {
        var result = CopilotRunner.IsIgnorableToolOutput("report_intent", "anything");

        Assert.True(result);
    }

    [Fact]
    public void IsIgnorableToolOutput_WithReportIntentCaseInsensitive_ReturnsTrue()
    {
        var result = CopilotRunner.IsIgnorableToolOutput("Report_Intent", "anything");

        Assert.True(result);
    }

    [Fact]
    public void IsIgnorableToolOutput_WithIntentLogged_ReturnsTrue()
    {
        var result = CopilotRunner.IsIgnorableToolOutput("some_tool", "Intent logged");

        Assert.True(result);
    }

    [Fact]
    public void IsIgnorableToolOutput_WithIntentLoggedAndWhitespace_ReturnsTrue()
    {
        var result = CopilotRunner.IsIgnorableToolOutput("some_tool", "  Intent logged  ");

        Assert.True(result);
    }

    [Fact]
    public void IsIgnorableToolOutput_WithNullToolName_ChecksOutput()
    {
        var result = CopilotRunner.IsIgnorableToolOutput(null, "Intent logged");

        Assert.True(result);
    }

    [Fact]
    public void IsIgnorableToolOutput_WithRegularOutput_ReturnsFalse()
    {
        var result = CopilotRunner.IsIgnorableToolOutput("bash", "command output here");

        Assert.False(result);
    }

    [Fact]
    public void IsIgnorableToolOutput_WithEmptyToolName_ChecksOutput()
    {
        var result = CopilotRunner.IsIgnorableToolOutput("", "regular output");

        Assert.False(result);
    }

    #endregion
}
