using Coralph;

namespace Coralph.Tests;

public sealed class LoopOptionsTests
{
    [Fact]
    public void DefaultModel_UsesLowercaseCopilotModelId()
    {
        var options = new LoopOptions();

        Assert.Equal("gpt-5.4", options.Model);
    }
}
