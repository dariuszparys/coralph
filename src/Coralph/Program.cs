using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Coralph;
using Microsoft.Extensions.Configuration;

var (overrides, err, initialConfig, configFile, showHelp) = ArgParser.Parse(args);
if (overrides is null)
{
    if (err is not null)
    {
        Console.Error.WriteLine(err);
        Console.Error.WriteLine();
    }

    var output = err is null ? Console.Out : Console.Error;
    ArgParser.PrintUsage(output);
    return showHelp && err is null ? 0 : 2;
}

if (initialConfig)
{
    var path = configFile ?? LoopOptions.ConfigurationFileName;
    if (File.Exists(path))
    {
        Console.Error.WriteLine($"Refusing to overwrite existing config file: {path}");
        return 1;
    }

    var defaultPayload = new Dictionary<string, LoopOptions>
    {
        [LoopOptions.ConfigurationSectionName] = new LoopOptions()
    };
    var json = JsonSerializer.Serialize(defaultPayload, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(path, json, CancellationToken.None);
    Console.WriteLine($"Wrote default configuration to {path}");
    return 0;
}

var opt = LoadOptions(overrides, configFile);

var banner = string.Join(Environment.NewLine, new[]
{
    "  _____                _       _",
    " / ____|              | |     | |",
    "| |     ___  _ __  ___| | ___ | |__",
    "| |    / _ \\\\| '_ \\\\/ __| |/ _ \\\\| '_ \\\\",
    "| |___| (_) | | | \\__ \\ | (_) | |_) |",
    " \\_____\\___/|_| |_|___/_|\\___/|_.__/"
});
Console.WriteLine(banner);
Console.WriteLine($"Coralph {GetVersionLabel()} | Model: {opt.Model}");

var ct = CancellationToken.None;

if (opt.RefreshIssues)
{
    Console.WriteLine("Refreshing issues...");
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
var minimumIterations = Math.Min(opt.MaxIterations, Math.Max(1, CountIssues(issues)));

for (var i = 1; i <= opt.MaxIterations; i++)
{
    Console.WriteLine($"\n=== Iteration {i}/{opt.MaxIterations} ===\n");

    var combinedPrompt = BuildCombinedPrompt(promptTemplate, issues, progress);

    string output;
    try
    {
        output = await CopilotRunner.RunOnceAsync(opt, combinedPrompt, ct);
    }
    catch (Exception ex)
    {
        output = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        Console.Error.WriteLine(output);
    }

    var entry = $"\n\n---\n# Iteration {i} ({DateTimeOffset.UtcNow:O})\n\nModel: {opt.Model}\n\n{output}\n";
    await File.AppendAllTextAsync(opt.ProgressFile, entry, ct);

    progress += entry;

    if (ContainsComplete(output) && i >= minimumIterations)
    {
        Console.WriteLine("\nCOMPLETE detected, stopping.\n");
        break;
    }
}

return 0;

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

    ApplyOverrides(options, overrides);
    return options;
}

static void ApplyOverrides(LoopOptions target, LoopOptionsOverrides overrides)
{
    if (overrides.MaxIterations is { } max) target.MaxIterations = max;
    if (!string.IsNullOrWhiteSpace(overrides.Model)) target.Model = overrides.Model;
    if (!string.IsNullOrWhiteSpace(overrides.PromptFile)) target.PromptFile = overrides.PromptFile;
    if (!string.IsNullOrWhiteSpace(overrides.ProgressFile)) target.ProgressFile = overrides.ProgressFile;
    if (!string.IsNullOrWhiteSpace(overrides.IssuesFile)) target.IssuesFile = overrides.IssuesFile;
    if (overrides.RefreshIssues is { } refresh) target.RefreshIssues = refresh;
    if (!string.IsNullOrWhiteSpace(overrides.Repo)) target.Repo = overrides.Repo;
    if (!string.IsNullOrWhiteSpace(overrides.CliPath)) target.CliPath = overrides.CliPath;
    if (!string.IsNullOrWhiteSpace(overrides.CliUrl)) target.CliUrl = overrides.CliUrl;
}

static string BuildCombinedPrompt(string promptTemplate, string issuesJson, string progress)
{
    var sb = new StringBuilder();

    sb.AppendLine("You are running inside a loop. Use the files and repository as your source of truth.");
    sb.AppendLine("Stop condition: when everything is done, output EXACTLY: <promise>COMPLETE</promise>.");
    sb.AppendLine();

    sb.AppendLine("# ISSUES_JSON");
    sb.AppendLine("```json");
    sb.AppendLine(issuesJson.Trim());
    sb.AppendLine("```");
    sb.AppendLine();

    sb.AppendLine("# PROGRESS_SO_FAR");
    sb.AppendLine("```text");
    sb.AppendLine(string.IsNullOrWhiteSpace(progress) ? "(empty)" : progress.Trim());
    sb.AppendLine("```");
    sb.AppendLine();

    sb.AppendLine("# INSTRUCTIONS");
    sb.AppendLine(promptTemplate.Trim());
    sb.AppendLine();

    sb.AppendLine("# OUTPUT_RULES");
    sb.AppendLine("- If you are done, output EXACTLY: <promise>COMPLETE</promise>");
    sb.AppendLine("- Otherwise, output what you changed and what you will do next iteration.");

    return sb.ToString();
}

static bool ContainsComplete(string output)
{
    if (output.Contains("<promise>COMPLETE</promise>", StringComparison.OrdinalIgnoreCase))
        return true;

    // Back-compat with older sentinel
    return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(l => string.Equals(l, "COMPLETE", StringComparison.OrdinalIgnoreCase));
}

static int CountIssues(string issuesJson)
{
    if (string.IsNullOrWhiteSpace(issuesJson))
        return 0;

    try
    {
        using var doc = JsonDocument.Parse(issuesJson);
        return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
    }
    catch (JsonException)
    {
        return 0;
    }
}

static string GetVersionLabel()
{
    var assembly = Assembly.GetExecutingAssembly();
    var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrWhiteSpace(info))
        return info;
    return assembly.GetName().Version?.ToString() ?? "unknown";
}
