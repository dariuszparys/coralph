using System.Text;
using GitHub.Copilot.SDK;
using Serilog;

namespace Coralph;

internal sealed class CopilotSessionRunner : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly CopilotSession _session;
    private readonly CopilotSessionEventRouter _router;
    private readonly IDisposable _subscription;
    private bool _started;
    private bool _disposed;
    private int _abortRequested;

    private CopilotSessionRunner(
        CopilotClient client,
        CopilotSession session,
        CopilotSessionEventRouter router,
        IDisposable subscription,
        bool started)
    {
        _client = client;
        _session = session;
        _router = router;
        _subscription = subscription;
        _started = started;
    }

    internal static async Task<CopilotSessionRunner> CreateAsync(LoopOptions opt, EventStreamWriter? eventStream)
    {
        var client = new CopilotClient(CopilotClientFactory.CreateClientOptions(opt));
        var started = false;
        CopilotSession? session = null;
        IDisposable? subscription = null;

        try
        {
            await client.StartAsync();
            started = true;

            var customTools = CustomTools.GetDefaultTools(opt.IssuesFile, opt.ProgressFile, opt.GeneratedTasksFile);
            var permissionPolicy = new PermissionPolicy(opt, eventStream);

            // OnUserInputRequest and OnPreToolUse/OnPostToolUse hooks are intentionally not set.
            // Coralph runs unattended; models must not prompt for user input during a loop iteration.
            // Tool-use events are already captured by CopilotSessionEventRouter via session.On().
            session = await client.CreateSessionAsync(
                CopilotClientFactory.CreateSessionConfig(opt, customTools, permissionPolicy.HandleAsync));

            var router = new CopilotSessionEventRouter(opt, eventStream, emitSessionEndOnIdle: false, emitSessionEndOnDispose: true);
            subscription = session.On(router.HandleEvent);

            return new CopilotSessionRunner(client, session, router, subscription, started);
        }
        catch
        {
            if (subscription is not null)
            {
                subscription.Dispose();
            }

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
                await DisposeAsync().ConfigureAwait(false);
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

        try
        {
            _subscription.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to dispose Copilot session subscription");
        }

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
