using Coralph;
using GitHub.Copilot.SDK;

namespace Coralph.Tests;

public sealed class CopilotSystemMessageFactoryTests
{
    [Fact]
    public void Create_UsesCustomizeModeWithExpectedSections()
    {
        var config = CopilotSystemMessageFactory.Create(new LoopOptions());

        Assert.Equal(SystemMessageMode.Customize, config.Mode);
        Assert.NotNull(config.Sections);
        Assert.Contains(SystemPromptSections.Tone, config.Sections.Keys);
        Assert.Contains(SystemPromptSections.Guidelines, config.Sections.Keys);
        Assert.Contains(SystemPromptSections.ToolInstructions, config.Sections.Keys);
        Assert.Contains(SystemPromptSections.Safety, config.Sections.Keys);
    }

    [Fact]
    public void Create_WithDryRun_AppendsDryRunSafetyInstruction()
    {
        var config = CopilotSystemMessageFactory.Create(new LoopOptions { DryRun = true });

        Assert.NotNull(config.Sections);
        Assert.True(config.Sections.TryGetValue(SystemPromptSections.Safety, out var safety));
        Assert.NotNull(safety);
        Assert.Contains("Dry-run mode is enabled", safety!.Content);
    }
}
