using Coralph;
using GitHub.Copilot;

namespace Coralph.Tests;

public sealed class CopilotSystemMessageFactoryTests
{
    [Fact]
    public void Create_UsesCustomizeModeWithExpectedSections()
    {
        var config = CopilotSystemMessageFactory.Create(new LoopOptions());

        Assert.Equal(SystemMessageMode.Customize, config.Mode);
        Assert.NotNull(config.Sections);
        Assert.Contains(SystemMessageSection.Tone, config.Sections.Keys);
        Assert.Contains(SystemMessageSection.Guidelines, config.Sections.Keys);
        Assert.Contains(SystemMessageSection.ToolInstructions, config.Sections.Keys);
        Assert.Contains(SystemMessageSection.Safety, config.Sections.Keys);
    }

    [Fact]
    public void Create_WithDryRun_AppendsDryRunSafetyInstruction()
    {
        var config = CopilotSystemMessageFactory.Create(new LoopOptions { DryRun = true });

        Assert.NotNull(config.Sections);
        Assert.True(config.Sections.TryGetValue(SystemMessageSection.Safety, out var safety));
        Assert.NotNull(safety);
        Assert.Contains("Dry-run mode is enabled", safety!.Content);
    }
}
