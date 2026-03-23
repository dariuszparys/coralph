using Serilog;
using Serilog.Context;

namespace Coralph;

internal sealed class LoopOrchestrator(LoopOptions opt, EventStreamWriter? eventStream)
{
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

        if (_opt.DockerSandbox && !inDockerSandbox)
        {
            var sandboxReady = await ValidateDockerHostAsync(ct).ConfigureAwait(false);
            if (!sandboxReady)
            {
                return 1;
            }
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
        var issuesRead = await _fileCache.TryReadTextAsync(_opt.IssuesFile, ct).ConfigureAwait(false);
        var issues = issuesRead.Exists ? issuesRead.Content : "[]";
        var progressRead = await _fileCache.TryReadTextAsync(_opt.ProgressFile, ct).ConfigureAwait(false);
        var progress = progressRead.Exists ? progressRead.Content : string.Empty;

        if (!PromptHelpers.TryGetHasOpenIssues(issues, out var hasOpenIssues, out var issuesError))
        {
            ConsoleOutput.WriteErrorLine(issuesError ?? "Failed to parse issues JSON.");
            return 1;
        }

        if (!hasOpenIssues)
        {
            Log.Information("No open issues found, exiting");
            ConsoleOutput.WriteLine(TerminalSignal.NoOpenIssues);
            TryCleanupGeneratedTasksFile(_opt.GeneratedTasksFile, "no open issues remained");
            await ConsoleOutput.WaitForAnyKeyToExitAsync("No open issues remain. Press Enter, Esc, or Q to close the TUI.", ct).ConfigureAwait(false);
            return 0;
        }

        var useDockerPerIteration = _opt.DockerSandbox && !inDockerSandbox;
        CopilotSessionRunner? sessionRunner = null;

        try
        {
            var stoppedByTerminalSignal = false;
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

                    progressRead = await _fileCache.TryReadTextAsync(_opt.ProgressFile, ct).ConfigureAwait(false);
                    progress = progressRead.Exists ? progressRead.Content : string.Empty;
                    issuesRead = await _fileCache.TryReadTextAsync(_opt.IssuesFile, ct).ConfigureAwait(false);
                    issues = issuesRead.Exists ? issuesRead.Content : "[]";
                    var generatedTasks = await TaskBacklog.EnsureBacklogAsync(issues, _opt.GeneratedTasksFile, ct).ConfigureAwait(false);
                    ConsoleOutput.RefreshGeneratedTasks();

                    if (_opt.DryRun)
                    {
                        ConsoleOutput.WriteLine("[DRY RUN] Planning tasks and building combined prompt...");
                    }

                    var combinedPrompt = PromptHelpers.BuildCombinedPrompt(promptTemplate, issues, progress, generatedTasks, dryRun: _opt.DryRun);

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

                    if (!PromptHelpers.TryGetTerminalSignal(output, out var terminalSignal))
                    {
                        continue;
                    }

                    if (string.Equals(terminalSignal, TerminalSignal.Complete, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var backlogContent = await File.ReadAllTextAsync(_opt.GeneratedTasksFile, ct).ConfigureAwait(false);
                            if (TaskBacklog.HasOpenTasks(backlogContent))
                            {
                                Log.Warning("COMPLETE signal ignored — open tasks remain in {BacklogFile}", _opt.GeneratedTasksFile);
                                ConsoleOutput.WriteWarningLine("COMPLETE signal ignored — open tasks remain in generated_tasks.json");
                                continue;
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            // No backlog file means no open tasks.
                        }
                    }

                    Log.Information("{TerminalSignal} detected at iteration {Iteration}, stopping loop", terminalSignal, i);
                    ConsoleOutput.WriteLine($"\n{terminalSignal} detected, stopping.\n");
                    await GitService.CommitProgressIfNeededAsync(_opt.ProgressFile, ct).ConfigureAwait(false);
                    if (BacklogCleanup.ShouldDeleteForTerminalSignal(terminalSignal))
                    {
                        TryCleanupGeneratedTasksFile(_opt.GeneratedTasksFile, $"terminal signal {terminalSignal}");
                    }
                    if (TerminalSignal.All.Contains(terminalSignal))
                    {
                        await ConsoleOutput.WaitForAnyKeyToExitAsync("No work remaining. Press Enter, Esc, or Q to close the TUI.", ct).ConfigureAwait(false);
                    }

                    stoppedByTerminalSignal = true;
                    break;
                }
            }

            if (!stoppedByTerminalSignal)
            {
                Log.Information(
                    "Max iterations reached ({MaxIterations}); keeping backlog file {BacklogFile} for resume",
                    _opt.MaxIterations,
                    _opt.GeneratedTasksFile);
            }
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

    private async Task<bool> ValidateDockerHostAsync(CancellationToken ct)
    {
        var dockerCheck = await DockerSandbox.CheckDockerAsync(ct).ConfigureAwait(false);
        if (!dockerCheck.Success)
        {
            ConsoleOutput.WriteErrorLine(dockerCheck.Message ?? "Docker is not available.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_opt.CliPath))
        {
            var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            var fullCliPath = Path.IsPathRooted(_opt.CliPath)
                ? Path.GetFullPath(_opt.CliPath)
                : Path.GetFullPath(Path.Combine(repoRoot, _opt.CliPath));
            if (!File.Exists(fullCliPath))
            {
                ConsoleOutput.WriteErrorLine($"Copilot CLI not found: {fullCliPath}");
                return false;
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(_opt.CliUrl))
        {
            return true;
        }

        var cliCheck = await DockerSandbox.CheckCopilotCliAsync(_opt.DockerImage, ct).ConfigureAwait(false);
        if (!cliCheck.Success)
        {
            ConsoleOutput.WriteErrorLine(cliCheck.Message ?? "Copilot CLI is not available in the Docker image.");
            return false;
        }

        return true;
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
