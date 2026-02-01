using System.Diagnostics;
using System.Text.Json;
using Coralph;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;

var (overrides, err, initialConfig, configFile, showHelp, showVersion) = ArgParser.Parse(args);

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

if (initialConfig)
{
    var path = ResolveConfigPath(configFile);
    if (File.Exists(path))
    {
        ConsoleOutput.WriteErrorLine($"Refusing to overwrite existing config file: {path}");
        return 1;
    }

    var defaultPayload = new Dictionary<string, LoopOptions>
    {
        [LoopOptions.ConfigurationSectionName] = new LoopOptions()
    };
    var json = JsonSerializer.Serialize(defaultPayload, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(path, json, CancellationToken.None);
    ConsoleOutput.WriteLine($"Wrote default configuration to {path}");
    return 0;
}

var opt = LoadOptions(overrides, configFile);

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
    ["prMode"] = opt.PrMode.ToString(),
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

static async Task<int> RunAsync(LoopOptions opt, EventStreamWriter? eventStream)
{
    var ct = CancellationToken.None;

    var inDockerSandbox = string.Equals(Environment.GetEnvironmentVariable(DockerSandbox.SandboxFlagEnv), "1", StringComparison.Ordinal);
    var combinedPromptFile = Environment.GetEnvironmentVariable(DockerSandbox.CombinedPromptEnv);
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
        }
    }

    if (!inDockerSandbox || string.IsNullOrWhiteSpace(combinedPromptFile))
    {
        // Display animated ASCII banner on startup
        await Banner.DisplayAnimatedAsync(ConsoleOutput.Out, ct);
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
            Log.Error(ex, "Docker sandbox iteration failed");
            ConsoleOutput.WriteErrorLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    // Detect repository and determine PR mode
    bool prModeActive = false;
    string? repoOwner = null;
    string? repoName = null;

    if (opt.PrMode == PrMode.Always)
    {
        prModeActive = true;
        ConsoleOutput.WriteLine("PR Mode: Always (forced via config/flag)");
    }
    else if (opt.PrMode == PrMode.Never)
    {
        prModeActive = false;
        ConsoleOutput.WriteLine("PR Mode: Disabled (forced via config/flag)");
    }
    else // PrMode.Auto
    {
        (repoOwner, repoName) = await GitPermissions.GetRepoFromGitRemoteAsync(ct);

        if (repoOwner is not null && repoName is not null)
        {
            var canPush = await GitPermissions.CanPushToMainAsync(repoOwner, repoName, ct);
            prModeActive = !canPush;
            var reason = $"auto-detected for {repoOwner}/{repoName}";

            if (prModeActive && opt.PrModeBypassUsers.Count > 0)
            {
                var login = await GitPermissions.GetCurrentUserLoginAsync(ct);
                if (GitPermissions.IsUserInBypassList(login, opt.PrModeBypassUsers))
                {
                    prModeActive = false;
                    reason = $"bypass user {login}";
                }
            }

            ConsoleOutput.WriteLine($"PR Mode: {(prModeActive ? "Enabled" : "Disabled")} ({reason})");
        }
        else
        {
            // No repo detected, default to direct push mode
            prModeActive = false;
            ConsoleOutput.WriteLine("PR Mode: Disabled (no GitHub repo detected)");
        }
    }

    ConsoleOutput.WriteLine();

    if (opt.RefreshIssues)
    {
        Log.Information("Refreshing issues from repository {Repo}", opt.Repo);
        ConsoleOutput.WriteLine("Refreshing issues...");
        var issuesJson = await GhIssues.FetchOpenIssuesJsonAsync(opt.Repo, ct);
        await File.WriteAllTextAsync(opt.IssuesFile, issuesJson, ct);
    }

    var promptTemplate = await File.ReadAllTextAsync(opt.PromptFile, ct);
    var issues = File.Exists(opt.IssuesFile)
        ? await File.ReadAllTextAsync(opt.IssuesFile, ct)
        : "[]";
    var progress = File.Exists(opt.ProgressFile)
        ? await File.ReadAllTextAsync(opt.ProgressFile, ct)
        : string.Empty;

    // Fetch PR feedback if in PR mode
    Dictionary<int, PrFeedbackData> prFeedbackByIssue = new();
    if (prModeActive && repoOwner is not null && repoName is not null)
    {
        prFeedbackByIssue = await FetchPrFeedbackForAllIssuesAsync(issues, repoOwner, repoName, ct);
    }

    if (!PromptHelpers.TryGetHasOpenIssues(issues, out var hasOpenIssues, out var issuesError))
    {
        ConsoleOutput.WriteErrorLine(issuesError ?? "Failed to parse issues JSON.");
        return 1;
    }

    if (!hasOpenIssues)
    {
        Log.Information("No open issues found, exiting");
        ConsoleOutput.WriteLine("NO_OPEN_ISSUES");
        return 0;
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
            progress = File.Exists(opt.ProgressFile)
                ? await File.ReadAllTextAsync(opt.ProgressFile, ct)
                : string.Empty;
            issues = File.Exists(opt.IssuesFile)
                ? await File.ReadAllTextAsync(opt.IssuesFile, ct)
                : "[]";

            var combinedPrompt = PromptHelpers.BuildCombinedPrompt(promptTemplate, issues, progress, prModeActive, prFeedbackByIssue);

            string output;
            string? turnError = null;
            var success = true;
            try
            {
                if (opt.DockerSandbox && !inDockerSandbox)
                {
                    output = await DockerSandbox.RunIterationAsync(opt, combinedPrompt, i, prModeActive, ct);
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
                Log.Information("{TerminalSignal} detected at iteration {Iteration}, stopping loop", terminalSignal, i);
                ConsoleOutput.WriteLine($"\n{terminalSignal} detected, stopping.\n");
                await CommitProgressIfNeededAsync(opt.ProgressFile, ct);
                break;
            }
        } // end LogContext scope
    }

    Log.Information("Coralph loop finished");
    return 0;
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
        return string.Empty;

    var output = await process.StandardOutput.ReadToEndAsync(ct);
    await process.WaitForExitAsync(ct);
    return output.Trim();
}

static LoopOptions LoadOptions(LoopOptionsOverrides overrides, string? configFile)
{
    var path = ResolveConfigPath(configFile);
    var options = new LoopOptions();

    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(path, optional: true, reloadOnChange: false)
            .Build();

        config.GetSection(LoopOptions.ConfigurationSectionName).Bind(options);
    }

    PromptHelpers.ApplyOverrides(options, overrides);
    return options;
}

static string ResolveConfigPath(string? configFile)
{
    var path = configFile ?? LoopOptions.ConfigurationFileName;
    if (Path.IsPathRooted(path))
        return path;

    var basePath = configFile is null
        ? AppContext.BaseDirectory
        : Directory.GetCurrentDirectory();

    return Path.Combine(basePath, path);
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

static async Task<Dictionary<int, PrFeedbackData>> FetchPrFeedbackForAllIssuesAsync(string issuesJson, string owner, string repo, CancellationToken ct)
{
    var result = new Dictionary<int, PrFeedbackData>();

    try
    {
        using var doc = JsonDocument.Parse(issuesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var issue in doc.RootElement.EnumerateArray())
        {
            if (!issue.TryGetProperty("number", out var numberProp) || !numberProp.TryGetInt32(out var issueNumber))
                continue;

            // Check if issue is still open
            if (issue.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String)
            {
                var stateValue = state.GetString();
                if (string.Equals(stateValue, "closed", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Find PRs for this issue
            var prNumbers = await PrFeedback.FindOpenPrsForIssueAsync(issueNumber, owner, repo, ct);
            if (prNumbers.Count == 0)
                continue;

            // Get feedback for the first open PR (usually there's only one)
            var feedback = await PrFeedback.GetPrFeedbackAsync(issueNumber, prNumbers[0], owner, repo, ct);
            if (feedback is not null)
            {
                result[issueNumber] = feedback;
                ConsoleOutput.WriteLine($"Found PR feedback for issue #{issueNumber} (PR #{feedback.PrNumber})");
            }
        }
    }
    catch (JsonException)
    {
        // Ignore parse errors
    }

    return result;
}
