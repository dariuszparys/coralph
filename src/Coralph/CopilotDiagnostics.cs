using System.Diagnostics;
using System.Text;
using System.Linq;

namespace Coralph;

internal static class CopilotDiagnostics
{
    private static readonly TimeSpan CliTimeout = TimeSpan.FromSeconds(6);
    private const int MaxOutputChars = 600;

    internal static bool IsCopilotCliDisconnect(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (IsDisconnectMessage(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    internal static async Task<string> CollectAsync(LoopOptions opt, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Copilot CLI diagnostics:");
        sb.AppendLine($"- Docker sandbox: {opt.DockerSandbox}");
        sb.AppendLine($"- In Docker sandbox: {IsInDockerSandbox()}");

        if (!string.IsNullOrWhiteSpace(opt.CliPath))
        {
            sb.AppendLine($"- CLI path: {opt.CliPath}");
        }

        if (!string.IsNullOrWhiteSpace(opt.CliUrl))
        {
            sb.AppendLine("- CLI URL configured");
        }

        if (!string.IsNullOrWhiteSpace(opt.CopilotConfigPath))
        {
            sb.AppendLine($"- Copilot config path option: {opt.CopilotConfigPath}");
        }

        AppendCopilotConfigStatus(sb);

        if (string.IsNullOrWhiteSpace(opt.CliUrl))
        {
            var cliPath = string.IsNullOrWhiteSpace(opt.CliPath) ? "copilot" : opt.CliPath;
            var versionResult = await TryRunCommandAsync(cliPath, new[] { "--version" }, ct);
            AppendCommandResult(sb, $"{cliPath} --version", versionResult);
        }

        return sb.ToString().TrimEnd();
    }

    internal static IReadOnlyList<string> GetHints(LoopOptions opt)
    {
        var hints = new List<string>
        {
            "The Copilot CLI process terminated during JSON-RPC (auth or CLI crash are common causes).",
            "Verify `copilot --version` works in the same environment as Coralph.",
            "If the CLI is not authenticated, run `copilot auth login` and retry."
        };

        if (opt.DockerSandbox)
        {
            hints.Add("When using --docker-sandbox, ensure the image includes Copilot CLI and mount auth config with --copilot-config-path ~/.copilot (mounted read-only by default). Pass --copilot-token explicitly if the container also needs a GitHub token.");
        }

        if (!string.IsNullOrWhiteSpace(opt.CliUrl))
        {
            hints.Add("If using --cli-url, confirm the CLI server is running and reachable.");
        }

        if (!string.IsNullOrWhiteSpace(opt.CliPath))
        {
            hints.Add($"Confirm the CLI path is correct: {opt.CliPath}");
        }

        return hints;
    }

    private static bool IsDisconnectMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (message.Contains("JSON-RPC", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("remote party", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return message.Contains("Copilot CLI", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInDockerSandbox()
    {
        return string.Equals(Environment.GetEnvironmentVariable(DockerSandbox.SandboxFlagEnv), "1", StringComparison.Ordinal);
    }

    private static void AppendCopilotConfigStatus(StringBuilder sb)
    {
        IEnumerable<string> candidates;
        if (IsInDockerSandbox())
        {
            candidates = new[] { "/home/vscode/.copilot", "/root/.copilot" };
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates = string.IsNullOrWhiteSpace(home)
                ? Array.Empty<string>()
                : new[] { Path.Combine(home, ".copilot") };
        }

        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            var exists = Directory.Exists(candidate);
            sb.AppendLine($"- Copilot config dir {candidate}: {(exists ? "found" : "missing")}");
        }
    }

    private static void AppendCommandResult(StringBuilder sb, string label, CommandResult result)
    {
        sb.Append($"- {label}: ");
        if (result.TimedOut)
        {
            sb.AppendLine("timed out");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Failure))
        {
            sb.AppendLine(result.Failure);
            return;
        }

        sb.AppendLine($"exit {result.ExitCode}");

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            sb.AppendLine($"  stdout: {result.Output}");
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            sb.AppendLine($"  stderr: {result.Error}");
        }
    }

    private static async Task<CommandResult> TryRunCommandAsync(string fileName, IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return new CommandResult(null, string.Empty, string.Empty, "failed to start process", TimedOut: false);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(CliTimeout);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // Best-effort cleanup.
                }

                return new CommandResult(null, await SafeReadAsync(stdoutTask), await SafeReadAsync(stderrTask), null, TimedOut: true);
            }

            var stdout = await SafeReadAsync(stdoutTask);
            var stderr = await SafeReadAsync(stderrTask);

            return new CommandResult(process.ExitCode, stdout, stderr, null, TimedOut: false);
        }
        catch (Exception ex)
        {
            return new CommandResult(null, string.Empty, string.Empty, ex.Message, TimedOut: false);
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            var output = await task;
            return TrimOutput(output);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string TrimOutput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= MaxOutputChars)
        {
            return trimmed;
        }

        return trimmed[..MaxOutputChars] + "... (truncated)";
    }

    private readonly record struct CommandResult(int? ExitCode, string Output, string Error, string? Failure, bool TimedOut);
}
