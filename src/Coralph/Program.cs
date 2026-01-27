using System.Diagnostics;
using System.Text.Json;
using Coralph;
using Microsoft.Extensions.Configuration;

var (overrides, err, initialConfig, configFile, showHelp) = ArgParser.Parse(args);
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
    var path = configFile ?? LoopOptions.ConfigurationFileName;
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

var ct = CancellationToken.None;

// Display animated ASCII banner on startup
await Banner.DisplayAnimatedAsync(ConsoleOutput.Out, ct);
ConsoleOutput.WriteLine();

if (opt.RefreshIssues)
{
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

if (!PromptHelpers.TryGetHasOpenIssues(issues, out var hasOpenIssues, out var issuesError))
{
    ConsoleOutput.WriteErrorLine(issuesError ?? "Failed to parse issues JSON.");
    return 1;
}

if (!hasOpenIssues)
{
    ConsoleOutput.WriteLine("NO_OPEN_ISSUES");
    return 0;
}
for (var i = 1; i <= opt.MaxIterations; i++)
{
    ConsoleOutput.WriteLine($"\n=== Iteration {i}/{opt.MaxIterations} ===\n");

    // Reload progress and issues before each iteration so assistant sees updates it made
    progress = File.Exists(opt.ProgressFile)
        ? await File.ReadAllTextAsync(opt.ProgressFile, ct)
        : string.Empty;
    issues = File.Exists(opt.IssuesFile)
        ? await File.ReadAllTextAsync(opt.IssuesFile, ct)
        : "[]";

    var combinedPrompt = PromptHelpers.BuildCombinedPrompt(promptTemplate, issues, progress);

    string output;
    try
    {
        output = await CopilotRunner.RunOnceAsync(opt, combinedPrompt, ct);
    }
    catch (Exception ex)
    {
        output = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        ConsoleOutput.WriteErrorLine(output);
    }

    // Progress is now managed by the assistant via tools (edit/bash) per prompt.md
    // The assistant writes clean, formatted summaries with learnings instead of raw output

    if (PromptHelpers.ContainsComplete(output))
    {
        ConsoleOutput.WriteLine("\nCOMPLETE detected, stopping.\n");
        await CommitProgressIfNeededAsync(opt.ProgressFile, ct);
        break;
    }
}

return 0;

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
    var path = configFile ?? LoopOptions.ConfigurationFileName;
    if (!Path.IsPathRooted(path))
        path = Path.Combine(AppContext.BaseDirectory, path);
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


