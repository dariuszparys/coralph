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
            Streaming = false,
        }))
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = new StringBuilder();

            using var sub = session.On(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageEvent msg:
                        if (!string.IsNullOrEmpty(msg.Data.Content))
                        {
                            output.AppendLine(msg.Data.Content);
                        }
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
