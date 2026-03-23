using Serilog;
using Coralph.Ui;

namespace Coralph;

internal static class LoopOptionsRuntimePreparation
{
    internal static bool TryPrepare(LoopOptions opt, out string? error)
    {
        error = null;

        if (opt.DemoMode)
        {
            opt.UiMode = UiMode.Tui;
            opt.StreamEvents = false;
        }

        if (opt.DryRun)
        {
            opt.StreamEvents = false;
            string[] dryRunDeny = ["edit*", "create_file", "delete_file", "write*", "run_in_terminal", "bash", "execute*", "shell"];
            opt.ToolDeny = [.. opt.ToolDeny, .. dryRunDeny];
        }

        var inDockerSandbox = string.Equals(Environment.GetEnvironmentVariable(DockerSandbox.SandboxFlagEnv), "1", StringComparison.Ordinal);
        if (inDockerSandbox || string.IsNullOrWhiteSpace(opt.CopilotConfigPath) || !opt.DockerSandbox)
        {
            return true;
        }

        var expanded = ExpandHomePath(opt.CopilotConfigPath);
        var fullConfigPath = Path.GetFullPath(expanded);
        if (!Directory.Exists(fullConfigPath))
        {
            error = $"Copilot config directory not found: {fullConfigPath}";
            return false;
        }

        opt.CopilotConfigPath = fullConfigPath;
        TryEnsureCopilotCacheDirectory(fullConfigPath);
        return true;
    }

    private static string ExpandHomePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (!path.StartsWith("~/", StringComparison.Ordinal))
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home) ? path : Path.Combine(home, path[2..]);
    }

    private static void TryEnsureCopilotCacheDirectory(string configPath)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(configPath, "pkg"));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to ensure Copilot cache directory under {Path}", configPath);
        }
    }
}
