using System.Text;
using GitHub.Copilot.SDK;

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
                        Console.WriteLine($"\n[Tool: {toolStart.Data.ToolName}]");
                        break;
                    case ToolExecutionCompleteEvent toolComplete:
                        // Show tool output in console
                        var toolOutput = toolComplete.Data.Result?.Content;
                        if (!string.IsNullOrWhiteSpace(toolOutput))
                        {
                            // Truncate very long outputs for readability
                            const int maxLen = 2000;
                            var display = toolOutput.Length > maxLen
                                ? toolOutput[..maxLen] + "\n... (truncated)"
                                : toolOutput;
                            Console.WriteLine(display);
                        }
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
}
