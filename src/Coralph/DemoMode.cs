using System.Text.Json;

namespace Coralph;

internal static class DemoMode
{
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(350);

    internal static async Task<int> RunAsync(LoopOptions opt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(opt);

        var originalTasksFile = opt.GeneratedTasksFile;
        string? demoTasksFile = null;
        try
        {
            demoTasksFile = await WriteDemoTasksAsync(ct).ConfigureAwait(false);
            opt.GeneratedTasksFile = demoTasksFile;
            ConsoleOutput.RefreshGeneratedTasks();

            if (ConsoleOutput.UsesTui)
            {
                await Banner.DisplayAnimatedInOutputAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await Banner.DisplayAnimatedAsync(ConsoleOutput.Out, ct).ConfigureAwait(false);
            }

            ConsoleOutput.WriteLine();
            ConsoleOutput.WriteLine("DEMO MODE: Sample data only. No external tools or files will be modified.");
            ConsoleOutput.WriteLine("Explore the UI and press Ctrl+C to exit.");
            ConsoleOutput.WriteLine();

            await EmitDemoTranscriptAsync(opt, ct).ConfigureAwait(false);
            await ConsoleOutput.WaitForAnyKeyToExitAsync("Demo mode active. Press any key to exit.", ct)
                .ConfigureAwait(false);
            return 0;
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to create demo tasks: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteErrorLine($"Failed to create demo tasks: {ex.Message}");
            return 1;
        }
        finally
        {
            opt.GeneratedTasksFile = originalTasksFile;
            if (!string.IsNullOrWhiteSpace(demoTasksFile))
            {
                TryDeleteDemoFile(demoTasksFile);
            }
        }
    }

    private static async Task<string> WriteDemoTasksAsync(CancellationToken ct)
    {
        var payload = new
        {
            version = 1,
            generatedAtUtc = DateTimeOffset.UtcNow,
            sourceIssueCount = 1,
            tasks = new[]
            {
                new
                {
                    id = "demo-001",
                    stableKey = "demo:preview-ui",
                    issueNumber = 0,
                    issueTitle = "Demo Issue",
                    title = "[DEMO] Preview UI behavior",
                    description = "Explore the demo experience.\n- Review mock tasks\n- Observe tool events\n- Inspect transcript styling",
                    status = "in_progress",
                    origin = "demo",
                    order = 1
                },
                new
                {
                    id = "demo-002",
                    stableKey = "demo:inspect-tasks",
                    issueNumber = 0,
                    issueTitle = "Demo Issue",
                    title = "[DEMO] Inspect the tasks pane",
                    description = "Focus on layout and selection behavior.",
                    status = "open",
                    origin = "demo",
                    order = 2
                },
                new
                {
                    id = "demo-003",
                    stableKey = "demo:verify-summary",
                    issueNumber = 0,
                    issueTitle = "Demo Issue",
                    title = "[DEMO] Review completion signals",
                    description = "Completed demo step to show status styling.",
                    status = "done",
                    origin = "demo",
                    order = 3
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(Path.GetTempPath(), $"coralph-demo-tasks-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        return path;
    }

    private static async Task EmitDemoTranscriptAsync(LoopOptions opt, CancellationToken ct)
    {
        ConsoleOutput.WriteSectionSeparator("Demo Session");
        ConsoleOutput.WriteLine("[demo] Booting mock run...");
        await DelayAsync(ct).ConfigureAwait(false);

        ConsoleOutput.WriteToolStart("list_open_issues");
        await DelayAsync(ct).ConfigureAwait(false);
        ConsoleOutput.WriteToolComplete("list_open_issues", "Loaded 1 demo issue");
        await DelayAsync(ct).ConfigureAwait(false);

        ConsoleOutput.WriteToolStart("list_generated_tasks");
        await DelayAsync(ct).ConfigureAwait(false);
        ConsoleOutput.WriteToolComplete("list_generated_tasks", "Loaded 3 demo tasks");
        await DelayAsync(ct).ConfigureAwait(false);

        ConsoleOutput.WriteAssistant("I'll walk through the demo backlog and summarize the plan.");
        await DelayAsync(ct).ConfigureAwait(false);

        if (opt.ShowReasoning)
        {
            ConsoleOutput.WriteReasoning("[demo] Reasoning output is visible when enabled.");
            await DelayAsync(ct).ConfigureAwait(false);
        }

        ConsoleOutput.WriteAssistant("Demo mode does not execute tools or write to your repository.");
        await DelayAsync(ct).ConfigureAwait(false);

        ConsoleOutput.WriteSectionSeparator("Demo Complete");
        ConsoleOutput.WriteLine("Waiting for input...");
    }

    private static Task DelayAsync(CancellationToken ct) => Task.Delay(StepDelay, ct);

    private static void TryDeleteDemoFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            ConsoleOutput.WriteWarningLine($"Warning: failed to delete demo tasks file '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ConsoleOutput.WriteWarningLine($"Warning: failed to delete demo tasks file '{path}': {ex.Message}");
        }
    }
}
