using System.Diagnostics;
using System.Text.Json;
using Coralph;
using Coralph.Ui;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;

var (overrides, err, init, configFile, showHelp, showVersion) = ArgParser.Parse(args);

var requestedUiMode = ResolveRequestedUiModeFromArgs(args);
var requestedStreamEvents = ResolveStreamEventsFromArgs(args);
var initialUiMode = showHelp || showVersion || overrides is null
    ? UiMode.Classic
    : UiModeResolver.Resolve(
        requestedUiMode,
        requestedStreamEvents,
        Console.IsInputRedirected,
        Console.IsOutputRedirected,
        Console.IsErrorRedirected);
await ConsoleOutput.ConfigureForModeAsync(
    initialUiMode,
    new LoopOptions
    {
        UiMode = requestedUiMode,
        StreamEvents = requestedStreamEvents
    });

try
{
    if (showVersion)
    {
        ConsoleOutput.WriteLine($"Coralph {Banner.GetVersion()}");
        return 0;
    }

    if (overrides is null)
    {
        if (err is not null)
        {
            ConsoleOutput.WriteErrorLine(err);
            ConsoleOutput.WriteErrorLine();
        }

        var output = err is null ? ConsoleOutput.OutWriter : ConsoleOutput.ErrorWriter;
        ArgParser.PrintUsage(output);
        return showHelp && err is null ? 0 : 2;
    }

    if (!string.IsNullOrWhiteSpace(overrides.WorkingDir))
    {
        if (!WorkingDirectoryContext.TryApply(overrides.WorkingDir, out var repoRoot, out var workingDirError))
        {
            ConsoleOutput.WriteErrorLine(workingDirError);
            return 2;
        }

        ConsoleOutput.WriteLine($"Using working directory: {repoRoot}");
    }

    if (init)
    {
        var initExit = await InitWorkflow.RunAsync(configFile);
        return initExit;
    }

    var opt = ConfigurationService.LoadOptions(overrides, configFile);
    if (opt.DemoMode)
    {
        opt.UiMode = UiMode.Tui;
        opt.StreamEvents = false;
    }
    var effectiveUiMode = UiModeResolver.Resolve(opt);
    await ConsoleOutput.ConfigureForModeAsync(effectiveUiMode, opt);

    EventStreamWriter? eventStream = null;
    if (opt.StreamEvents)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        eventStream = new EventStreamWriter(Console.Out, sessionId);
        eventStream.WriteSessionHeader(Directory.GetCurrentDirectory());

        var errorConsole = ConsoleOutput.CreateConsole(Console.Error, Console.IsErrorRedirected);
        ConsoleOutput.Configure(errorConsole, errorConsole);
    }

    // Configure structured logging
    Logging.Configure(opt);
    Log.Information("Coralph starting with Model={Model}, MaxIterations={MaxIterations}", opt.Model, opt.MaxIterations);

    eventStream?.Emit("agent_start", fields: new Dictionary<string, object?>
    {
        ["model"] = opt.Model,
        ["maxIterations"] = opt.MaxIterations,
        ["version"] = Banner.GetVersion(),
        ["showReasoning"] = opt.ShowReasoning,
        ["colorizedOutput"] = opt.ColorizedOutput
    });

    var exitCode = 1;
    try
    {
        exitCode = await RunAsync(opt, eventStream);
        return exitCode;
    }
    finally
    {
        eventStream?.Emit("agent_end", fields: new Dictionary<string, object?>
        {
            ["exitCode"] = exitCode
        });
        Logging.Close();
    }
}
finally
{
    await ConsoleOutput.DisposeBackendAsync();
}

static async Task<int> RunAsync(LoopOptions opt, EventStreamWriter? eventStream)
{
    using var cts = new CancellationTokenSource();
    using var stopHandlerScope = ConsoleOutput.PushStopRequestHandler(() =>
    {
        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
        }
    });

    ConsoleCancelEventHandler cancelKeyPressHandler = (_, e) =>
    {
        e.Cancel = true;
        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
        }
    };

    Console.CancelKeyPress += cancelKeyPressHandler;

    var ct = cts.Token;
    var emittedCopilotDiagnostics = false;
    var fileCache = FileContentCache.Shared;

    try
    {
        var inDockerSandbox = string.Equals(Environment.GetEnvironmentVariable(DockerSandbox.SandboxFlagEnv), "1", StringComparison.Ordinal);
        var combinedPromptFile = Environment.GetEnvironmentVariable(DockerSandbox.CombinedPromptEnv);
        if (opt.DemoMode)
        {
            return await DemoMode.RunAsync(opt, ct);
        }
        if (opt.ListModels)
        {
            if (opt.DockerSandbox && !inDockerSandbox)
            {
                ConsoleOutput.WriteLine("Note: --list-models runs on the host environment; --docker-sandbox is ignored.");
            }

            try
            {
                var models = await CopilotModelDiscovery.ListModelsAsync(opt, ct);
                if (opt.ListModelsJson)
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
                emittedCopilotDiagnostics = await TryEmitCopilotDiagnosticsAsync(ex, opt, ct, emittedCopilotDiagnostics);
                Log.Error(ex, "Failed to list Copilot models");
                ConsoleOutput.WriteErrorLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }
        if (opt.DockerSandbox && !inDockerSandbox)
        {
            var dockerCheck = await DockerSandbox.CheckDockerAsync(ct);
            if (!dockerCheck.Success)
            {
                ConsoleOutput.WriteErrorLine(dockerCheck.Message ?? "Docker is not available.");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(opt.CliPath))
            {
                var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
                var fullCliPath = Path.IsPathRooted(opt.CliPath)
                    ? Path.GetFullPath(opt.CliPath)
                    : Path.GetFullPath(Path.Combine(repoRoot, opt.CliPath));
                if (!File.Exists(fullCliPath))
                {
                    ConsoleOutput.WriteErrorLine($"Copilot CLI not found: {fullCliPath}");
                    return 1;
                }
            }
            else if (string.IsNullOrWhiteSpace(opt.CliUrl))
            {
                var cliCheck = await DockerSandbox.CheckCopilotCliAsync(opt.DockerImage, ct);
                if (!cliCheck.Success)
                {
                    ConsoleOutput.WriteErrorLine(cliCheck.Message ?? "Copilot CLI is not available in the Docker image.");
                    return 1;
                }
            }

            if (!string.IsNullOrWhiteSpace(opt.CopilotConfigPath))
            {
                var expanded = ExpandHomePath(opt.CopilotConfigPath);
                var fullConfigPath = Path.GetFullPath(expanded);
                if (!Directory.Exists(fullConfigPath))
                {
                    ConsoleOutput.WriteErrorLine($"Copilot config directory not found: {fullConfigPath}");
                    return 1;
                }
                opt.CopilotConfigPath = fullConfigPath;
                TryEnsureCopilotCacheDirectory(fullConfigPath);
            }
        }

        if (!inDockerSandbox || string.IsNullOrWhiteSpace(combinedPromptFile))
        {
            if (ConsoleOutput.UsesTui)
            {
                await Banner.DisplayAnimatedInOutputAsync(ct);
            }
            else
            {
                // Display animated ASCII banner on startup
                await Banner.DisplayAnimatedAsync(ConsoleOutput.Out, ct);
            }

            ConsoleOutput.WriteLine();
        }

        if (!string.IsNullOrWhiteSpace(combinedPromptFile))
        {
            if (!File.Exists(combinedPromptFile))
            {
                ConsoleOutput.WriteErrorLine($"Combined prompt file not found: {combinedPromptFile}");
                return 1;
            }

            try
            {
                var combinedPrompt = await File.ReadAllTextAsync(combinedPromptFile, ct);
                await CopilotRunner.RunOnceAsync(opt, combinedPrompt, ct, eventStream, turn: 1);
                return 0;
            }
            catch (Exception ex)
            {
                emittedCopilotDiagnostics = await TryEmitCopilotDiagnosticsAsync(ex, opt, ct, emittedCopilotDiagnostics);
                Log.Error(ex, "Docker sandbox iteration failed");
                ConsoleOutput.WriteErrorLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        if (opt.RefreshIssues)
        {
            Log.Information("Refreshing issues from repository {Repo}", opt.Repo);
            ConsoleOutput.WriteLine("Refreshing issues from GitHub...");
            var issuesJson = await GhIssues.FetchOpenIssuesJsonAsync(opt.Repo, ct);
            await File.WriteAllTextAsync(opt.IssuesFile, issuesJson, ct);
            fileCache.Invalidate(opt.IssuesFile);
        }
        else if (opt.RefreshIssuesAzdo)
        {
            Log.Information("Refreshing work items from Azure Boards (Organization={Organization}, Project={Project})",
                opt.AzdoOrganization ?? "(default)", opt.AzdoProject ?? "(default)");
            ConsoleOutput.WriteLine("Refreshing work items from Azure Boards...");
            var issuesJson = await AzBoards.FetchOpenWorkItemsJsonAsync(opt.AzdoOrganization, opt.AzdoProject, ct);
            await File.WriteAllTextAsync(opt.IssuesFile, issuesJson, ct);
            fileCache.Invalidate(opt.IssuesFile);
        }

        if (!StartupValidation.TryValidatePromptFile(opt.PromptFile, out var promptValidationError))
        {
            ConsoleOutput.WriteErrorLine(promptValidationError ?? "Prompt file validation failed.");
            return 1;
        }

        var promptTemplate = await File.ReadAllTextAsync(opt.PromptFile, ct);
        var issuesRead = await fileCache.TryReadTextAsync(opt.IssuesFile, ct);
        var issues = issuesRead.Exists ? issuesRead.Content : "[]";
        var progressRead = await fileCache.TryReadTextAsync(opt.ProgressFile, ct);
        var progress = progressRead.Exists ? progressRead.Content : string.Empty;
        string generatedTasks;

        if (!PromptHelpers.TryGetHasOpenIssues(issues, out var hasOpenIssues, out var issuesError))
        {
            ConsoleOutput.WriteErrorLine(issuesError ?? "Failed to parse issues JSON.");
            return 1;
        }

        if (!hasOpenIssues)
        {
            Log.Information("No open issues found, exiting");
            ConsoleOutput.WriteLine("NO_OPEN_ISSUES");
            TryCleanupGeneratedTasksFile(opt.GeneratedTasksFile, "no open issues remained");
            await ConsoleOutput.WaitForAnyKeyToExitAsync("No open issues remain. Press any key to close the TUI.", ct);
            return 0;
        }

        var useDockerPerIteration = opt.DockerSandbox && !inDockerSandbox;
        CopilotSessionRunner? sessionRunner = null;

        try
        {
            var stoppedByTerminalSignal = false;
            if (!useDockerPerIteration)
            {
                try
                {
                    sessionRunner = await CopilotSessionRunner.CreateAsync(opt, eventStream);
                }
                catch (Exception ex)
                {
                    emittedCopilotDiagnostics = await TryEmitCopilotDiagnosticsAsync(ex, opt, ct, emittedCopilotDiagnostics);
                    Log.Error(ex, "Failed to start Copilot session");
                    ConsoleOutput.WriteErrorLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                    return 1;
                }
            }

            for (var i = 1; i <= opt.MaxIterations; i++)
            {
                eventStream?.Emit("turn_start", turn: i, fields: new Dictionary<string, object?>
                {
                    ["maxIterations"] = opt.MaxIterations
                });

                using (LogContext.PushProperty("Iteration", i))
                {
                    Log.Information("Starting iteration {Iteration} of {MaxIterations}", i, opt.MaxIterations);
                    ConsoleOutput.WriteLine($"\n=== Iteration {i}/{opt.MaxIterations} ===\n");

                    // Reload progress and issues before each iteration so assistant sees updates it made
                    progressRead = await fileCache.TryReadTextAsync(opt.ProgressFile, ct);
                    progress = progressRead.Exists ? progressRead.Content : string.Empty;
                    issuesRead = await fileCache.TryReadTextAsync(opt.IssuesFile, ct);
                    issues = issuesRead.Exists ? issuesRead.Content : "[]";
                    generatedTasks = await TaskBacklog.EnsureBacklogAsync(issues, opt.GeneratedTasksFile, ct);
                    ConsoleOutput.RefreshGeneratedTasks();

                    var combinedPrompt = PromptHelpers.BuildCombinedPrompt(promptTemplate, issues, progress, generatedTasks);

                    string output;
                    string? turnError = null;
                    var success = true;
                    try
                    {
                        if (useDockerPerIteration)
                        {
                            output = await DockerSandbox.RunIterationAsync(opt, combinedPrompt, i, ct);
                        }
                        else if (sessionRunner is not null)
                        {
                            output = await sessionRunner.RunTurnAsync(combinedPrompt, ct, i);
                        }
                        else
                        {
                            output = await CopilotRunner.RunOnceAsync(opt, combinedPrompt, ct, eventStream, i);
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
                        emittedCopilotDiagnostics = await TryEmitCopilotDiagnosticsAsync(ex, opt, ct, emittedCopilotDiagnostics);
                    }

                    // Progress is now managed by the assistant via tools (edit/bash) per prompt.md
                    // The assistant writes clean, formatted summaries with learnings instead of raw output

                    var hasTerminalSignal = PromptHelpers.TryGetTerminalSignal(output, out var terminalSignal);
                    eventStream?.Emit("turn_end", turn: i, fields: new Dictionary<string, object?>
                    {
                        ["success"] = success,
                        ["output"] = output,
                        ["error"] = turnError,
                        ["terminalSignal"] = hasTerminalSignal ? terminalSignal : null
                    });

                    if (hasTerminalSignal)
                    {
                        if (string.Equals(terminalSignal, "COMPLETE", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var backlogContent = await File.ReadAllTextAsync(opt.GeneratedTasksFile, ct);
                                if (TaskBacklog.HasOpenTasks(backlogContent))
                                {
                                    Log.Warning("COMPLETE signal ignored — open tasks remain in {BacklogFile}", opt.GeneratedTasksFile);
                                    ConsoleOutput.WriteWarningLine("COMPLETE signal ignored — open tasks remain in generated_tasks.json");
                                    continue;
                                }
                            }
                            catch (FileNotFoundException)
                            {
                                // No backlog file means no open tasks — allow COMPLETE
                            }
                        }

                        Log.Information("{TerminalSignal} detected at iteration {Iteration}, stopping loop", terminalSignal, i);
                        ConsoleOutput.WriteLine($"\n{terminalSignal} detected, stopping.\n");
                        await CommitProgressIfNeededAsync(opt.ProgressFile, ct);
                        if (BacklogCleanup.ShouldDeleteForTerminalSignal(terminalSignal))
                        {
                            TryCleanupGeneratedTasksFile(opt.GeneratedTasksFile, $"terminal signal {terminalSignal}");
                        }
                        if (ShouldWaitForAnyKeyToExit(terminalSignal))
                        {
                            await ConsoleOutput.WaitForAnyKeyToExitAsync("No work remaining. Press any key to close the TUI.", ct);
                        }

                        stoppedByTerminalSignal = true;
                        break;
                    }
                } // end LogContext scope
            }

            if (!stoppedByTerminalSignal)
            {
                Log.Information(
                    "Max iterations reached ({MaxIterations}); keeping backlog file {BacklogFile} for resume",
                    opt.MaxIterations,
                    opt.GeneratedTasksFile);
            }
        }
        finally
        {
            if (sessionRunner is not null)
            {
                await sessionRunner.DisposeAsync();
            }
        }

        Log.Information("Coralph loop finished");
        return 0;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        Log.Information("Cancellation requested, stopping Coralph loop");
        ConsoleOutput.WriteWarningLine("Cancellation requested, stopping.");
        return 130;
    }
    finally
    {
        Console.CancelKeyPress -= cancelKeyPressHandler;
    }
}

static async Task CommitProgressIfNeededAsync(string progressFile, CancellationToken ct)
{
    if (!File.Exists(progressFile))
        return;

    // Check if progress file has uncommitted changes
    var statusResult = await RunGitAsync($"status --porcelain -- \"{progressFile}\"", ct);
    if (string.IsNullOrWhiteSpace(statusResult))
        return; // No changes to commit

    // Stage and commit the progress file
    await RunGitAsync($"add \"{progressFile}\"", ct);
    var commitResult = await RunGitAsync("commit -m \"chore: update progress.txt\"", ct);
    if (!string.IsNullOrWhiteSpace(commitResult))
        ConsoleOutput.WriteLine($"Auto-committed {progressFile}");
}

static bool ShouldWaitForAnyKeyToExit(string terminalSignal)
{
    return string.Equals(terminalSignal, "COMPLETE", StringComparison.OrdinalIgnoreCase)
           || string.Equals(terminalSignal, "ALL_TASKS_COMPLETE", StringComparison.OrdinalIgnoreCase)
           || string.Equals(terminalSignal, "NO_OPEN_ISSUES", StringComparison.OrdinalIgnoreCase);
}

static void TryCleanupGeneratedTasksFile(string backlogFile, string reason)
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

static async Task<string> RunGitAsync(string arguments, CancellationToken ct)
{
    var psi = new ProcessStartInfo("git", arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi);
    if (process is null)
    {
        Log.Warning("Failed to start git for arguments: {Arguments}", arguments);
        return string.Empty;
    }

    var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
    var stderrTask = process.StandardError.ReadToEndAsync(ct);
    await process.WaitForExitAsync(ct);

    var output = await stdoutTask;
    var error = await stderrTask;

    if (process.ExitCode != 0)
    {
        var trimmedError = error?.Trim();
        Log.Warning("git {Arguments} failed with exit code {ExitCode}: {Error}", arguments, process.ExitCode,
            string.IsNullOrWhiteSpace(trimmedError) ? "(no error output)" : trimmedError);
        return string.Empty;
    }

    return output.Trim();
}

static async Task<bool> TryEmitCopilotDiagnosticsAsync(Exception ex, LoopOptions opt, CancellationToken ct, bool alreadyEmitted)
{
    if (alreadyEmitted || ct.IsCancellationRequested)
    {
        return alreadyEmitted;
    }

    if (!CopilotDiagnostics.IsCopilotCliDisconnect(ex))
    {
        return alreadyEmitted;
    }

    try
    {
        var diagnostics = await CopilotDiagnostics.CollectAsync(opt, ct);
        if (!string.IsNullOrWhiteSpace(diagnostics))
        {
            ConsoleOutput.WriteErrorLine();
            ConsoleOutput.WriteErrorLine(diagnostics);
        }

        var hints = CopilotDiagnostics.GetHints(opt);
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

    return true;
}

static UiMode ResolveRequestedUiModeFromArgs(string[] cliArgs)
{
    var mode = UiMode.Auto;
    for (var i = 0; i < cliArgs.Length; i++)
    {
        var arg = cliArgs[i];
        if (arg.StartsWith("--ui=", StringComparison.Ordinal))
        {
            var value = arg["--ui=".Length..];
            if (UiModeParser.TryParse(value, out var parsed))
            {
                mode = parsed;
            }

            continue;
        }

        if (!string.Equals(arg, "--ui", StringComparison.Ordinal))
        {
            continue;
        }

        if (i + 1 >= cliArgs.Length)
        {
            continue;
        }

        if (UiModeParser.TryParse(cliArgs[i + 1], out var parsedNext))
        {
            mode = parsedNext;
        }
    }

    return mode;
}

static bool ResolveStreamEventsFromArgs(string[] cliArgs)
{
    var enabled = false;
    for (var i = 0; i < cliArgs.Length; i++)
    {
        var arg = cliArgs[i];
        if (arg.StartsWith("--stream-events=", StringComparison.Ordinal))
        {
            if (TryParseBoolToken(arg["--stream-events=".Length..], out var parsed))
            {
                enabled = parsed;
            }
            continue;
        }

        if (arg.StartsWith("--event-stream=", StringComparison.Ordinal))
        {
            if (TryParseBoolToken(arg["--event-stream=".Length..], out var parsed))
            {
                enabled = parsed;
            }
            continue;
        }

        if (!string.Equals(arg, "--stream-events", StringComparison.Ordinal) &&
            !string.Equals(arg, "--event-stream", StringComparison.Ordinal))
        {
            continue;
        }

        if (i + 1 < cliArgs.Length && TryParseBoolToken(cliArgs[i + 1], out var parsedNext))
        {
            enabled = parsedNext;
        }
        else
        {
            enabled = true;
        }
    }

    return enabled;
}

static bool TryParseBoolToken(string? token, out bool value)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        value = false;
        return false;
    }

    if (bool.TryParse(token, out value))
    {
        return true;
    }

    if (string.Equals(token, "1", StringComparison.Ordinal))
    {
        value = true;
        return true;
    }

    if (string.Equals(token, "0", StringComparison.Ordinal))
    {
        value = false;
        return true;
    }

    return false;
}


static string ExpandHomePath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return path;

    if (path == "~")
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    if (path.StartsWith("~/", StringComparison.Ordinal))
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            return path;

        return Path.Combine(home, path[2..]);
    }

    return path;
}

static void TryEnsureCopilotCacheDirectory(string configPath)
{
    try
    {
        var pkgPath = Path.Combine(configPath, "pkg");
        Directory.CreateDirectory(pkgPath);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to ensure Copilot cache directory under {Path}", configPath);
    }
}
