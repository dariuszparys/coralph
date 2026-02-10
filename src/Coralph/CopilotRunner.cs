using System.Text;
using System.Linq;
using Serilog;
using GitHub.Copilot.SDK;

namespace Coralph;

internal static class CopilotRunner
{
    internal static async Task<string> RunOnceAsync(LoopOptions opt, string prompt, CancellationToken ct, EventStreamWriter? eventStream = null, int? turn = null)
    {
        var clientOptions = new CopilotClientOptions
        {
            Cwd = Directory.GetCurrentDirectory(),
        };

        if (!string.IsNullOrWhiteSpace(opt.CliPath)) clientOptions.CliPath = opt.CliPath;
        if (!string.IsNullOrWhiteSpace(opt.CliUrl)) clientOptions.CliUrl = opt.CliUrl;
        if (!string.IsNullOrWhiteSpace(opt.CopilotToken)) clientOptions.GithubToken = opt.CopilotToken;

        await using var client = new CopilotClient(clientOptions);
        var started = false;
        string result;
        try
        {
            await client.StartAsync();
            started = true;

            var customTools = CustomTools.GetDefaultTools(opt.IssuesFile, opt.ProgressFile, opt.GeneratedTasksFile);
            var permissionPolicy = new PermissionPolicy(opt, eventStream);

            await using (var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = opt.Model,
                Streaming = true,
                Tools = customTools,
                OnPermissionRequest = permissionPolicy.HandleAsync,
                Provider = ProviderConfigFactory.Create(opt)
            }))
            {
                var router = new CopilotSessionEventRouter(opt, eventStream, emitSessionEndOnIdle: true, emitSessionEndOnDispose: false);
                var state = router.StartTurn(turn);
                using var sub = session.On(router.HandleEvent);
                await session.SendAsync(new MessageOptions { Prompt = prompt });

                try
                {
                    using (ct.Register(() => state.Done.TrySetCanceled(ct)))
                    {
                        await state.Done.Task;
                    }
                }
                finally
                {
                    router.EndTurn();
                }

                result = state.Output.ToString().Trim();
            }
        }
        finally
        {
            if (started)
            {
                try
                {
                    await client.StopAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to stop Copilot client");
                }
            }
        }

        return result;
    }

    internal static string SummarizeToolOutput(string toolOutput)
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

    internal static bool IsIgnorableToolOutput(string? toolName, string toolOutput)
    {
        if (!string.IsNullOrWhiteSpace(toolName) &&
            string.Equals(toolName, "report_intent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(toolOutput.Trim(), "Intent logged", StringComparison.OrdinalIgnoreCase);
    }
}
