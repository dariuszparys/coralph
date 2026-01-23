using System.Text;
using System.Linq;
using GitHub.Copilot.SDK;
using Spectre.Console;

namespace Coralph;

internal static class CopilotRunner
{
    internal static async Task<string> RunOnceAsync(LoopOptions opt, string prompt, CancellationToken ct)
    {
        var clientOptions = new CopilotClientOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
        };

        if (!string.IsNullOrWhiteSpace(opt.CliPath)) clientOptions.CliPath = opt.CliPath;
        if (!string.IsNullOrWhiteSpace(opt.CliUrl)) clientOptions.CliUrl = opt.CliUrl;

        await using var client = new CopilotClient(clientOptions);
        await client.StartAsync();

        string result;
        await using (var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = opt.Model,
            Streaming = true,
            OnPermissionRequest = (request, invocation) =>
                Task.FromResult(new PermissionRequestResult { Kind = "approved" }),
        }))
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = new StringBuilder();

            string? lastToolName = null;
            using var sub = session.On(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        Console.Write(delta.Data.DeltaContent);
                        output.Append(delta.Data.DeltaContent);
                        break;
                    case AssistantReasoningDeltaEvent reasoning:
                        // Stream reasoning to console only (not saved to progress)
                        Console.Write(reasoning.Data.DeltaContent);
                        break;
                    case ToolExecutionStartEvent toolStart:
                        lastToolName = toolStart.Data.ToolName;
                        WriteToolHeader($"[Tool: {toolStart.Data.ToolName}]");
                        break;
                    case ToolExecutionCompleteEvent toolComplete:
                        // Show tool output in console
                        var toolOutput = toolComplete.Data.Result?.Content;
                        if (!string.IsNullOrWhiteSpace(toolOutput))
                        {
                            if (IsIgnorableToolOutput(lastToolName, toolOutput))
                            {
                                lastToolName = null;
                                break;
                            }
                            var display = SummarizeToolOutput(toolOutput);
                            Console.WriteLine(display);
                        }
                        lastToolName = null;
                        break;
                    case AssistantMessageEvent:
                    case AssistantReasoningEvent:
                        Console.WriteLine();
                        break;
                    case SessionErrorEvent err:
                        done.TrySetException(new InvalidOperationException(err.Data.Message));
                        break;
                    case SessionIdleEvent:
                        done.TrySetResult();
                        break;
                }
            });

            await session.SendAsync(new MessageOptions { Prompt = prompt });

            using (ct.Register(() => done.TrySetCanceled(ct)))
            {
                await done.Task;
            }

            result = output.ToString().Trim();
        }

        await client.StopAsync();
        return result;
    }

    private static void WriteToolHeader(string text)
    {
        WriteToolHeader(text, Console.IsOutputRedirected);
    }

    internal static void WriteToolHeader(string text, bool isOutputRedirected)
    {
        if (isOutputRedirected)
        {
            Console.WriteLine(text);
            return;
        }

        var escaped = Markup.Escape(text);
        AnsiConsole.MarkupLine($"[black on orange1] {escaped} [/]");
    }

    private static string SummarizeToolOutput(string toolOutput)
    {
        var normalized = toolOutput.Replace("\r\n", "\n").TrimEnd();
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        var lines = normalized.Split('\n');
        var totalLines = lines.Length;
        var totalChars = normalized.Length;

        const int maxLines = 6;
        const int maxChars = 800;
        var previewLines = lines.Take(maxLines);
        var preview = string.Join('\n', previewLines);

        if (preview.Length > maxChars)
        {
            preview = preview[..maxChars] + "\n... (truncated)";
        }

        if (totalLines > maxLines || preview.Length < totalChars)
        {
            return $"{preview}\n... ({totalLines} lines, {totalChars} chars)";
        }

        return preview;
    }

    private static bool IsIgnorableToolOutput(string? toolName, string toolOutput)
    {
        if (!string.IsNullOrWhiteSpace(toolName) &&
            string.Equals(toolName, "report_intent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(toolOutput.Trim(), "Intent logged", StringComparison.OrdinalIgnoreCase);
    }
}
