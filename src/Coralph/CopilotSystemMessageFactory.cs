using GitHub.Copilot.SDK;

namespace Coralph;

internal static class CopilotSystemMessageFactory
{
    internal static SystemMessageConfig Create(LoopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dryRunInstruction = options.DryRun
            ? "\n- Dry-run mode is enabled. Do not write files, apply patches, or create commits; provide a preview of intended changes only."
            : string.Empty;

        return new SystemMessageConfig
        {
            Mode = SystemMessageMode.Customize,
            Sections = new Dictionary<string, SectionOverride>
            {
                [SystemPromptSections.Tone] = new()
                {
                    Action = SectionOverrideAction.Append,
                    Content = "\n- Be concise, direct, and execution-focused."
                },
                [SystemPromptSections.Guidelines] = new()
                {
                    Action = SectionOverrideAction.Append,
                    Content =
                        "\n- Coralph runs unattended; continue autonomously until the requested task is complete." +
                        "\n- Match the repository's existing conventions, libraries, and coding style." +
                        "\n- Prefer small, targeted changes that preserve existing behavior unless the task requires broader edits."
                },
                [SystemPromptSections.ToolInstructions] = new()
                {
                    Action = SectionOverrideAction.Append,
                    Content =
                        "\n- Prefer Coralph's internal read-only tools and available repository context before broader exploration when they can answer the question." +
                        "\n- Do not stop to ask for interactive input during a loop run."
                },
                [SystemPromptSections.Safety] = new()
                {
                    Action = SectionOverrideAction.Append,
                    Content =
                        "\n- Respect Coralph runtime constraints and avoid destructive or system-wide side effects." +
                        dryRunInstruction
                }
            }
        };
    }
}
