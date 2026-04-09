using Serilog;
using Serilog.Context;

namespace Coralph;

internal sealed class LoopOrchestrator(LoopOptions opt, EventStreamWriter? eventStream)
{
    private const int MaxConsecutiveStalledIterations = 2;

    private readonly LoopOptions _opt = opt ?? throw new ArgumentNullException(nameof(opt));
    private readonly EventStreamWriter? _eventStream = eventStream;
    private readonly FileContentCache _fileCache = FileContentCache.Shared;
    private bool _emittedCopilotDiagnostics;

    internal async Task<int> RunAsync(CancellationTokenSource runCancellation)
    {
        ArgumentNullException.ThrowIfNull(runCancellation);

        var ct = runCancellation.Token;
        using var stopHandlerScope = ConsoleOutput.PushStopRequestHandler(() =>
        {
            if (!runCancellation.IsCancellationRequested)
            {
                runCancellation.Cancel();
            }
        });

        using var backendMonitorCts = new CancellationTokenSource();
        var backendMonitorTask = ConsoleOutputSupervisor.Start(runCancellation, backendMonitorCts.Token);

        try
        {
            return await RunCoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Log.Information("Cancellation requested, stopping Coralph loop");
            ConsoleOutput.WriteWarningLine("Cancellation requested, stopping.");
            return 130;
        }
        finally
        {
            backendMonitorCts.Cancel();
            if (backendMonitorTask is not null)
            {
                try
                {
                    await backendMonitorTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (backendMonitorCts.IsCancellationRequested)
                {
                    // Expected when the run finishes before the backend exits.
                }
            }
        }
    }

    private async Task<int> RunCoreAsync(CancellationToken ct)
    {
        var inDockerSandbox = string.Equals(Environment.GetEnvironmentVariable(DockerSandbox.SandboxFlagEnv), "1", StringComparison.Ordinal);
        var combinedPromptFile = Environment.GetEnvironmentVariable(DockerSandbox.CombinedPromptEnv);

        if (_opt.DemoMode)
        {
            return await DemoMode.RunAsync(_opt, ct).ConfigureAwait(false);
        }

        if (_opt.ListModels)
        {
            return await ListModelsAsync(inDockerSandbox, ct).ConfigureAwait(false);
        }

        string? dockerUnavailableReason = null;
        if (_opt.DockerSandbox && !inDockerSandbox)
        {
            dockerUnavailableReason = await ValidateDockerHostAsync(ct).ConfigureAwait(false);
        }

        var (useDockerPerIteration, dockerFallbackReason) = ResolveDockerExecution(_opt.DockerSandbox, inDockerSandbox, dockerUnavailableReason);
        if (!string.IsNullOrWhiteSpace(dockerFallbackReason))
        {
            _opt.DockerSandbox = false;
            Log.Warning("Docker sandbox unavailable, falling back to host execution: {Reason}", dockerFallbackReason);
            ConsoleOutput.WriteWarningLine($"Docker sandbox unavailable: {dockerFallbackReason}");
            ConsoleOutput.WriteWarningLine("Falling back to host execution for this run.");
            ConsoleOutput.WriteLine();
        }

        if (!inDockerSandbox || string.IsNullOrWhiteSpace(combinedPromptFile))
        {
            if (ConsoleOutput.UsesTui)
            {
                await Banner.DisplayAnimatedInOutputAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await Banner.DisplayAnimatedAsync(ConsoleOutput.Out, ct).ConfigureAwait(false);
            }

            ConsoleOutput.WriteLine();
        }

        if (!string.IsNullOrWhiteSpace(combinedPromptFile))
        {
            return await RunCombinedPromptFileAsync(combinedPromptFile, ct).ConfigureAwait(false);
        }

        await RefreshIssuesIfRequestedAsync(ct).ConfigureAwait(false);

        if (!StartupValidation.TryValidatePromptFile(_opt.PromptFile, out var promptValidationError))
        {
            ConsoleOutput.WriteErrorLine(promptValidationError ?? "Prompt file validation failed.");
            return 1;
        }

        var promptTemplate = await File.ReadAllTextAsync(_opt.PromptFile, ct).ConfigureAwait(false);
        InvalidateLoopArtifactCache();
        var initialState = await LoopIterationState.CaptureAsync(_opt, _fileCache, ct).ConfigureAwait(false);

        if (!PromptHelpers.TryGetHasOpenIssues(initialState.IssuesJson, out var hasOpenIssues, out var issuesError))
        {
            ConsoleOutput.WriteErrorLine(issuesError ?? "Failed to parse issues JSON.");
            return 1;
        }

        if (!hasOpenIssues)
        {
            return await FinishForTerminalSignalAsync(TerminalSignal.NoOpenIssues, iteration: 0, ct).ConfigureAwait(false);
        }

        CopilotSessionRunner? sessionRunner = null;

        try
        {
            var consecutiveStalledIterations = 0;
            if (!useDockerPerIteration)
            {
                try
                {
                    sessionRunner = await CopilotSessionRunner.CreateAsync(_opt, _eventStream).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await EmitCopilotDiagnosticsIfNeededAsync(ex, ct).ConfigureAwait(false);
                    Log.Error(ex, "Failed to start Copilot session");
                    ConsoleOutput.WriteErrorLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                    return 1;
                }
            }

            for (var i = 1; i <= _opt.MaxIterations; i++)
            {
                _eventStream?.Emit("turn_start", turn: i, fields: new Dictionary<string, object?> { ["maxIterations"] = _opt.MaxIterations });

                using (LogContext.PushProperty("Iteration", i))
                {
                    Log.Information("Starting iteration {Iteration} of {MaxIterations}", i, _opt.MaxIterations);
                    ConsoleOutput.WriteLine($"\n=== Iteration {i}/{_opt.MaxIterations} ===\n");

                    if (_opt.DryRun)
                    {
                        ConsoleOutput.WriteLine($"[DRY RUN] ╔══════════════════════════════════════════╗");
                        ConsoleOutput.WriteLine($"[DRY RUN] ║  DRY RUN PREVIEW — iteration {i}/{_opt.MaxIterations}");
                        ConsoleOutput.WriteLine($"[DRY RUN] ║  No files will be modified. No commits.");
                        ConsoleOutput.WriteLine($"[DRY RUN] ╚══════════════════════════════════════════╝");
                    }

                    if (i > 1)
                    {
                        await RefreshIssuesIfRequestedAsync(ct).ConfigureAwait(false);
                    }

                    InvalidateLoopArtifactCache();
                    var issuesRead = await _fileCache.TryReadTextAsync(_opt.IssuesFile, ct).ConfigureAwait(false);
                    var issues = issuesRead.Exists ? issuesRead.Content : "[]";
                    await TaskBacklog.EnsureBacklogAsync(issues, _opt.GeneratedTasksFile, ct).ConfigureAwait(false);
                    ConsoleOutput.RefreshGeneratedTasks();

                    var preTurnState = await LoopIterationState.CaptureAsync(_opt, _fileCache, ct, invalidateArtifacts: true).ConfigureAwait(false);
                    if (preTurnState.TryGetImplicitTerminalSignal(out var preTurnSignal, out var preTurnError))
                    {
                        return await FinishForTerminalSignalAsync(preTurnSignal, i, ct).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrWhiteSpace(preTurnError))
                    {
                        ConsoleOutput.WriteErrorLine(preTurnError);
                        return 1;
                    }

                    if (_opt.DryRun)
                    {
                        ConsoleOutput.WriteLine("[DRY RUN] Planning tasks and building combined prompt...");
                    }

                    var combinedPrompt = PromptHelpers.BuildCombinedPrompt(
                        promptTemplate,
                        preTurnState.IssuesJson,
                        preTurnState.ProgressText,
                        preTurnState.GeneratedTasksJson,
                        dryRun: _opt.DryRun);

                    string output;
                    string? turnError = null;
                    var success = true;
                    try
                    {
                        if (_opt.DryRun)
                        {
                            ConsoleOutput.WriteLine("[DRY RUN] Calling Copilot to generate code changes (preview mode)...");
                        }

                        output = useDockerPerIteration
                            ? await DockerSandbox.RunIterationAsync(_opt, combinedPrompt, i, ct).ConfigureAwait(false)
                            : sessionRunner is not null
                                ? await sessionRunner.RunTurnAsync(combinedPrompt, ct, i).ConfigureAwait(false)
                                : await CopilotRunner.RunOnceAsync(_opt, combinedPrompt, ct, _eventStream, i).ConfigureAwait(false);

                        if (_opt.DryRun)
                        {
                            ConsoleOutput.WriteLine("[DRY RUN] Preview complete. No files were written and no git commands were run.");
                        }

                        Log.Information("Iteration {Iteration} completed successfully", i);
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        turnError = $"{ex.GetType().Name}: {ex.Message}";
                        output = $"ERROR: {turnError}";
                        Log.Error(ex, "Iteration {Iteration} failed with error", i);
                        ConsoleOutput.WriteErrorLine(output);
                        await EmitCopilotDiagnosticsIfNeededAsync(ex, ct).ConfigureAwait(false);
                    }

                    _eventStream?.Emit("turn_end", turn: i, fields: new Dictionary<string, object?>
                    {
                        ["success"] = success,
                        ["output"] = output,
                        ["error"] = turnError,
                        ["terminalSignal"] = PromptHelpers.TryGetTerminalSignal(output, out var signalForEvent) ? signalForEvent : null
                    });

                    var postTurnState = await LoopIterationState.CaptureAsync(_opt, _fileCache, ct, invalidateArtifacts: true).ConfigureAwait(false);
                    if (postTurnState.TryGetImplicitTerminalSignal(out var implicitSignal, out var implicitSignalError))
                    {
                        return await FinishForTerminalSignalAsync(implicitSignal, i, ct).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrWhiteSpace(implicitSignalError))
                    {
                        ConsoleOutput.WriteErrorLine(implicitSignalError);
                        return 1;
                    }

                    if (!PromptHelpers.TryGetTerminalSignal(output, out var terminalSignal))
                    {
                        if (postTurnState.HasMeaningfulChangesComparedTo(preTurnState))
                        {
                            consecutiveStalledIterations = 0;
                            continue;
                        }

                        consecutiveStalledIterations++;
                        Log.Warning(
                            "Iteration {Iteration} made no meaningful progress ({StalledIterations}/{MaxStalledIterations})",
                            i,
                            consecutiveStalledIterations,
                            MaxConsecutiveStalledIterations);
                        ConsoleOutput.WriteWarningLine(
                            $"Iteration {i} made no meaningful progress ({consecutiveStalledIterations}/{MaxConsecutiveStalledIterations}).");

                        if (consecutiveStalledIterations < MaxConsecutiveStalledIterations)
                        {
                            continue;
                        }

                        ConsoleOutput.WriteErrorLine(
                            "Stopping Coralph because consecutive iterations left issues, backlog, progress, and git state unchanged.");
                        Log.Warning(
                            "Stopping Coralph after {MaxStalledIterations} consecutive stalled iterations",
                            MaxConsecutiveStalledIterations);
                        return 1;
                    }

                    consecutiveStalledIterations = 0;

                    if (!IsTerminalSignalStillValid(terminalSignal, postTurnState))
                    {
                        Log.Warning("{TerminalSignal} ignored at iteration {Iteration} because work still remains", terminalSignal, i);
                        ConsoleOutput.WriteWarningLine($"{terminalSignal} signal ignored — repository state still shows remaining work.");
                        continue;
                    }

                    return await FinishForTerminalSignalAsync(terminalSignal, i, ct).ConfigureAwait(false);
                }
            }

            Log.Information(
                "Max iterations reached ({MaxIterations}); keeping backlog file {BacklogFile} for resume",
                _opt.MaxIterations,
                _opt.GeneratedTasksFile);
        }
        finally
        {
            if (sessionRunner is not null)
            {
                await sessionRunner.DisposeAsync().ConfigureAwait(false);
            }
        }

        Log.Information("Coralph loop finished");
        return 0;
    }

    private async Task<int> ListModelsAsync(bool inDockerSandbox, CancellationToken ct)
    {
        if (_opt.DockerSandbox && !inDockerSandbox)
        {
            ConsoleOutput.WriteLine("Note: --list-models runs on the host environment; --docker-sandbox is ignored.");
        }

        try
        {
            var models = await CopilotModelDiscovery.ListModelsAsync(_opt, ct).ConfigureAwait(false);
            if (_opt.ListModelsJson)
            {
                CopilotModelDiscovery.WriteModelsJson(models);
            }
            else
            {
                CopilotModelDiscovery.WriteModels(models);
            }

            return 0;
        }
        catch (Exception ex)
        {
            await EmitCopilotDiagnosticsIfNeededAsync(ex, ct).ConfigureAwait(false);
            Log.Error(ex, "Failed to list Copilot models");
            ConsoleOutput.WriteErrorLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    internal static (bool UseDockerPerIteration, string? FallbackReason) ResolveDockerExecution(bool dockerSandboxRequested, bool inDockerSandbox, string? unavailableReason)
    {
        if (!dockerSandboxRequested || inDockerSandbox)
        {
            return (false, null);
        }

        if (string.IsNullOrWhiteSpace(unavailableReason))
        {
            return (true, null);
        }

        return (false, unavailableReason.Trim());
    }

    private async Task<string?> ValidateDockerHostAsync(CancellationToken ct)
    {
        var dockerCheck = await DockerSandbox.CheckDockerAsync(ct).ConfigureAwait(false);
        if (!dockerCheck.Success)
        {
            return dockerCheck.Message ?? "Docker is not available.";
        }

        if (!string.IsNullOrWhiteSpace(_opt.CliPath))
        {
            var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            var fullCliPath = Path.IsPathRooted(_opt.CliPath)
                ? Path.GetFullPath(_opt.CliPath)
                : Path.GetFullPath(Path.Combine(repoRoot, _opt.CliPath));
            if (!File.Exists(fullCliPath))
            {
                return $"Copilot CLI not found: {fullCliPath}";
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(_opt.CliUrl))
        {
            return null;
        }

        var cliCheck = await DockerSandbox.CheckCopilotCliAsync(_opt.DockerImage, ct).ConfigureAwait(false);
        if (!cliCheck.Success)
        {
            return cliCheck.Message ?? "Copilot CLI is not available in the Docker image.";
        }

        return null;
    }

    private async Task<int> RunCombinedPromptFileAsync(string combinedPromptFile, CancellationToken ct)
    {
        if (!File.Exists(combinedPromptFile))
        {
            ConsoleOutput.WriteErrorLine($"Combined prompt file not found: {combinedPromptFile}");
            return 1;
        }

        try
        {
            var combinedPrompt = await File.ReadAllTextAsync(combinedPromptFile, ct).ConfigureAwait(false);
            await CopilotRunner.RunOnceAsync(_opt, combinedPrompt, ct, _eventStream, turn: 1).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            await EmitCopilotDiagnosticsIfNeededAsync(ex, ct).ConfigureAwait(false);
            Log.Error(ex, "Docker sandbox iteration failed");
            ConsoleOutput.WriteErrorLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private async Task RefreshIssuesIfRequestedAsync(CancellationToken ct)
    {
        if (_opt.RefreshIssues)
        {
            Log.Information("Refreshing issues from repository {Repo}", _opt.Repo);
            ConsoleOutput.WriteLine(_opt.DryRun ? "[DRY RUN] Fetching open issues from GitHub..." : "Refreshing issues from GitHub...");
            var issuesJson = await GhIssues.FetchOpenIssuesJsonAsync(_opt.Repo, ct).ConfigureAwait(false);
            if (!_opt.DryRun)
            {
                await File.WriteAllTextAsync(_opt.IssuesFile, issuesJson, ct).ConfigureAwait(false);
                _fileCache.Invalidate(_opt.IssuesFile);
            }

            return;
        }

        if (!_opt.RefreshIssuesAzdo)
        {
            return;
        }

        Log.Information(
            "Refreshing work items from Azure Boards (Organization={Organization}, Project={Project})",
            _opt.AzdoOrganization ?? "(default)",
            _opt.AzdoProject ?? "(default)");
        ConsoleOutput.WriteLine(_opt.DryRun
            ? "[DRY RUN] Fetching open work items from Azure Boards..."
            : "Refreshing work items from Azure Boards...");
        var workItemsJson = await AzBoards.FetchOpenWorkItemsJsonAsync(_opt.AzdoOrganization, _opt.AzdoProject, ct).ConfigureAwait(false);
        if (_opt.DryRun)
        {
            return;
        }

        await File.WriteAllTextAsync(_opt.IssuesFile, workItemsJson, ct).ConfigureAwait(false);
        _fileCache.Invalidate(_opt.IssuesFile);
    }

    private async Task<int> FinishForTerminalSignalAsync(string terminalSignal, int iteration, CancellationToken ct)
    {
        if (iteration > 0)
        {
            Log.Information("{TerminalSignal} detected at iteration {Iteration}, stopping loop", terminalSignal, iteration);
        }
        else
        {
            Log.Information("{TerminalSignal} detected before iteration processing, stopping loop", terminalSignal);
        }

        ConsoleOutput.WriteLine($"\n{terminalSignal} detected, stopping.\n");
        await GitService.CommitProgressIfNeededAsync(_opt.ProgressFile, ct).ConfigureAwait(false);
        if (BacklogCleanup.ShouldDeleteForTerminalSignal(terminalSignal))
        {
            TryCleanupGeneratedTasksFile(_opt.GeneratedTasksFile, $"terminal signal {terminalSignal}");
        }

        if (TerminalSignal.All.Contains(terminalSignal))
        {
            var message = string.Equals(terminalSignal, TerminalSignal.NoOpenIssues, StringComparison.OrdinalIgnoreCase)
                ? "No open issues remain. Press Enter, Esc, or Q to close the TUI."
                : "No work remaining. Press Enter, Esc, or Q to close the TUI.";
            await ConsoleOutput.WaitForAnyKeyToExitAsync(message, ct).ConfigureAwait(false);
        }

        Log.Information("Coralph loop finished");
        return 0;
    }

    private void InvalidateLoopArtifactCache()
    {
        _fileCache.Invalidate(_opt.IssuesFile);
        _fileCache.Invalidate(_opt.ProgressFile);
        _fileCache.Invalidate(_opt.GeneratedTasksFile);
    }

    internal static bool IsTerminalSignalStillValid(string terminalSignal, LoopIterationState state)
    {
        if (string.Equals(terminalSignal, TerminalSignal.NoOpenIssues, StringComparison.OrdinalIgnoreCase))
        {
            return PromptHelpers.TryGetHasOpenIssues(state.IssuesJson, out var hasOpenIssues, out _) && !hasOpenIssues;
        }

        if (string.Equals(terminalSignal, TerminalSignal.AllTasksComplete, StringComparison.OrdinalIgnoreCase))
        {
            return !TaskBacklog.HasOpenTasks(state.GeneratedTasksJson);
        }

        if (string.Equals(terminalSignal, TerminalSignal.Complete, StringComparison.OrdinalIgnoreCase))
        {
            return !TaskBacklog.HasOpenTasks(state.GeneratedTasksJson);
        }

        return true;
    }

    private async Task EmitCopilotDiagnosticsIfNeededAsync(Exception ex, CancellationToken ct)
    {
        if (_emittedCopilotDiagnostics || ct.IsCancellationRequested || !CopilotDiagnostics.IsCopilotCliDisconnect(ex))
        {
            return;
        }

        try
        {
            var diagnostics = await CopilotDiagnostics.CollectAsync(_opt, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(diagnostics))
            {
                ConsoleOutput.WriteErrorLine();
                ConsoleOutput.WriteErrorLine(diagnostics);
            }

            var hints = CopilotDiagnostics.GetHints(_opt);
            if (hints.Count > 0)
            {
                ConsoleOutput.WriteErrorLine();
                ConsoleOutput.WriteErrorLine("Copilot CLI troubleshooting:");
                foreach (var hint in hints)
                {
                    ConsoleOutput.WriteErrorLine($"- {hint}");
                }
            }
        }
        catch (Exception diagEx)
        {
            Log.Warning(diagEx, "Failed to emit Copilot CLI diagnostics");
        }

        _emittedCopilotDiagnostics = true;
    }

    private static void TryCleanupGeneratedTasksFile(string backlogFile, string reason)
    {
        if (BacklogCleanup.TryDelete(backlogFile, out var deleteError))
        {
            Log.Information("Deleted backlog file {BacklogFile} after {Reason}", backlogFile, reason);
            ConsoleOutput.RefreshGeneratedTasks();
            return;
        }

        if (deleteError is null)
        {
            return;
        }

        Log.Warning(deleteError, "Failed to delete backlog file {BacklogFile} after {Reason}", backlogFile, reason);
        ConsoleOutput.WriteWarningLine($"Warning: failed to delete generated tasks backlog '{backlogFile}': {deleteError.Message}");
    }
}
