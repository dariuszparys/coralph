using GitHub.Copilot.SDK;
using Serilog;

namespace Coralph;

internal sealed class CopilotSessionRunner : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly CopilotSession _session;
    private readonly CopilotSessionEventRouter _router;
    private bool _started;
    private bool _disposed;
    private int _abortRequested;

    private CopilotSessionRunner(
        CopilotClient client,
        CopilotSession session,
        CopilotSessionEventRouter router,
        bool started)
    {
        _client = client;
        _session = session;
        _router = router;
        _started = started;
    }

    internal static async Task<CopilotSessionRunner> CreateAsync(LoopOptions opt, EventStreamWriter? eventStream)
    {
        var client = new CopilotClient(CopilotClientFactory.CreateClientOptions(opt));
        var started = false;
        CopilotSession? session = null;

        try
        {
            await client.StartAsync();
            started = true;

            var customTools = CustomTools.GetDefaultTools(opt.IssuesFile, opt.ProgressFile, opt.GeneratedTasksFile);
            var permissionPolicy = new PermissionPolicy(opt, eventStream);
            var router = new CopilotSessionEventRouter(opt, eventStream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: true);

            // OnUserInputRequest and OnPreToolUse/OnPostToolUse hooks are intentionally not set.
            // Coralph runs unattended; models must not prompt for user input during a loop iteration.
            // Tool-use events are captured by CopilotSessionEventRouter via SessionConfig.OnEvent.
            session = await client.CreateSessionAsync(
                CopilotClientFactory.CreateSessionConfig(opt, customTools, permissionPolicy.HandleAsync, router.HandleEvent));

            return new CopilotSessionRunner(client, session, router, started);
        }
        catch
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

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

            await client.DisposeAsync();
            throw;
        }
    }

    internal async Task<string> RunTurnAsync(string prompt, CancellationToken ct, int turn)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CopilotSessionRunner));
        }

        ct.ThrowIfCancellationRequested();
        var state = _router.StartTurn(turn);
        try
        {
            using var cancelRegistration = ct.Register(() =>
            {
                state.Done.TrySetCanceled(ct);
                RequestAbort();
            });

            ct.ThrowIfCancellationRequested();
            await _session.SendAsync(new MessageOptions { Prompt = prompt });
            await state.Done.Task;

            return state.Output.ToString().Trim();
        }
        finally
        {
            _router.EndTurn();
        }
    }

    private void RequestAbort()
    {
        if (Interlocked.Exchange(ref _abortRequested, 1) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _session.AbortAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to abort Copilot session after cancellation");
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _router.EmitSessionEndIfNeeded("disposed");

        await _session.DisposeAsync();

        if (_started)
        {
            try
            {
                await _client.StopAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to stop Copilot client");
            }
        }

        await _client.DisposeAsync();
    }
}
