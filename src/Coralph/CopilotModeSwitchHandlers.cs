using GitHub.Copilot;
using Serilog;

namespace Coralph;

internal sealed class CopilotModeSwitchHandlers(LoopOptions options, EventStreamWriter? eventStream)
{
    private readonly LoopOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly EventStreamWriter? _eventStream = eventStream;

    internal Task<AutoModeSwitchResponse> HandleAutoModeSwitchRequestAsync(
        AutoModeSwitchRequest request,
        AutoModeSwitchInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(invocation);

        Log.Information(
            "Approving Copilot auto mode switch request for session {SessionId}; error code {ErrorCode}; retry after {RetryAfterSeconds}",
            invocation.SessionId,
            request.ErrorCode,
            request.RetryAfterSeconds);

        _eventStream?.Emit("auto_mode_switch_request", fields: new Dictionary<string, object?>
        {
            ["copilotSessionId"] = invocation.SessionId,
            ["errorCode"] = request.ErrorCode,
            ["retryAfterSeconds"] = request.RetryAfterSeconds,
            ["decision"] = "yes"
        });

        return Task.FromResult(AutoModeSwitchResponse.Yes);
    }

    internal Task<ExitPlanModeResult> HandleExitPlanModeRequestAsync(
        ExitPlanModeRequest request,
        ExitPlanModeInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(invocation);

        var approved = !_options.DryRun;
        var selectedAction = approved ? ResolveSelectedAction(request) : null;
        var feedback = approved
            ? null
            : "Dry-run mode is enabled; keep the response as a plan and do not modify files or commit.";

        Log.Information(
            "Handled Copilot exit plan mode request for session {SessionId}; approved {Approved}; dry run {DryRun}; selected action {SelectedAction}",
            invocation.SessionId,
            approved,
            _options.DryRun,
            selectedAction);

        _eventStream?.Emit("exit_plan_mode_request", fields: new Dictionary<string, object?>
        {
            ["copilotSessionId"] = invocation.SessionId,
            ["approved"] = approved,
            ["dryRun"] = _options.DryRun,
            ["recommendedAction"] = request.RecommendedAction,
            ["selectedAction"] = selectedAction,
            ["summary"] = request.Summary,
            ["actionCount"] = request.Actions?.Count ?? 0
        });

        return Task.FromResult(new ExitPlanModeResult
        {
            Approved = approved,
            Feedback = feedback,
            SelectedAction = selectedAction
        });
    }

    private static string? ResolveSelectedAction(ExitPlanModeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RecommendedAction))
        {
            return request.RecommendedAction;
        }

        return request.Actions?.FirstOrDefault(action => !string.IsNullOrWhiteSpace(action));
    }
}
