using System.Diagnostics;
using System.Reflection;
using System.Text;
using Serilog;

namespace Coralph;

internal record DockerCheckResult(bool Success, string? Message);
internal record DockerMount(string HostPath, string ContainerPath, bool ReadOnly);
internal record DockerLaunchInfo(string Command, List<string> Arguments, List<DockerMount> Mounts);

internal static class DockerSandbox
{
    internal const string SandboxFlagEnv = "CORALPH_DOCKER_SANDBOX";
    internal const string CombinedPromptEnv = "CORALPH_COMBINED_PROMPT_FILE";

    internal static async Task<DockerCheckResult> CheckDockerAsync(CancellationToken ct)
    {
        var versionResult = await RunDockerAsync("--version", ct);
        if (versionResult.ExitCode != 0)
        {
            return new DockerCheckResult(false, "Docker is not installed or not available on PATH.");
        }

        var infoResult = await RunDockerAsync("info --format \"{{json .}}\"", ct);
        if (infoResult.ExitCode != 0)
        {
            return new DockerCheckResult(false, "Docker is not running. Start Docker Desktop and try again.");
        }

        return new DockerCheckResult(true, null);
    }

    internal static async Task<string> RunIterationAsync(LoopOptions opt, string combinedPrompt, int iteration, bool prModeActive, CancellationToken ct)
    {
        var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var combinedPromptPath = Path.Combine(repoRoot, $".coralph-combined-prompt-{iteration}-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(combinedPromptPath, combinedPrompt, ct);

        try
        {
            var launchInfo = ResolveLaunchInfo(repoRoot);
            var args = BuildDockerRunArguments(opt, repoRoot, combinedPromptPath, prModeActive, launchInfo);
            var result = await RunDockerAsync(args, ct);
            WriteDockerOutput(result.Output, result.Error);
            var output = CombineOutput(result.Output, result.Error);
            if (result.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(result.Error) ? "Docker sandbox failed." : result.Error.Trim();
                throw new InvalidOperationException(error);
            }

            return output;
        }
        finally
        {
            TryDeleteFile(combinedPromptPath);
        }
    }

    private static DockerLaunchInfo ResolveLaunchInfo(string repoRoot)
    {
        var managedAssembly = ResolveAssemblyFromBaseDirectory();
        if (!string.IsNullOrWhiteSpace(managedAssembly))
        {
            return BuildDotnetLaunch(repoRoot, managedAssembly);
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            managedAssembly = ResolveManagedAssembly(processPath);
            if (!string.IsNullOrWhiteSpace(managedAssembly))
            {
                return BuildDotnetLaunch(repoRoot, managedAssembly);
            }

            if (!OperatingSystem.IsLinux())
            {
                throw new InvalidOperationException("Docker sandbox requires Coralph.dll when running on non-Linux hosts. Use `dotnet run` or `dotnet build` to enable the sandbox.");
            }

            return BuildBinaryLaunch(repoRoot, processPath);
        }

        throw new InvalidOperationException("Unable to resolve Coralph executable path for Docker sandbox.");
    }

    private static string BuildDockerRunArguments(LoopOptions opt, string repoRoot, string combinedPromptPath, bool prModeActive, DockerLaunchInfo launchInfo)
    {
        var output = new StringBuilder();
        output.Append("run --rm");
        output.Append(" --pull=missing");
        output.Append(" -v ");
        output.Append(Quote($"{repoRoot}:/repo"));
        output.Append(" -w /repo");
        foreach (var mount in launchInfo.Mounts)
        {
            output.Append(" -v ");
            output.Append(Quote($"{mount.HostPath}:{mount.ContainerPath}{(mount.ReadOnly ? ":ro" : string.Empty)}"));
        }
        output.Append(" -e ");
        output.Append(Quote($"{SandboxFlagEnv}=1"));
        output.Append(" -e ");
        output.Append(Quote($"{CombinedPromptEnv}={MapPathToContainer(repoRoot, combinedPromptPath, "Combined prompt file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: true)}"));

        output.Append(' ');
        output.Append(Quote(opt.DockerImage));
        output.Append(' ');
        output.Append(Quote(launchInfo.Command));
        foreach (var argument in launchInfo.Arguments)
        {
            output.Append(' ');
            output.Append(Quote(argument));
        }
        output.Append(" --max-iterations 1");
        output.Append(" --prompt-file ");
        output.Append(Quote(MapPathToContainer(repoRoot, opt.PromptFile, "Prompt file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: true)));
        output.Append(" --progress-file ");
        output.Append(Quote(MapPathToContainer(repoRoot, opt.ProgressFile, "Progress file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: false)));
        output.Append(" --issues-file ");
        output.Append(Quote(MapPathToContainer(repoRoot, opt.IssuesFile, "Issues file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: true)));
        output.Append(" --model ");
        output.Append(Quote(opt.Model));
        output.Append(" --show-reasoning ");
        output.Append(opt.ShowReasoning.ToString().ToLowerInvariant());
        output.Append(" --colorized-output ");
        output.Append(opt.ColorizedOutput.ToString().ToLowerInvariant());
        output.Append(" --stream-events false");
        output.Append(" --pr-mode ");
        output.Append(Quote(prModeActive ? PrMode.Always.ToString() : PrMode.Never.ToString()));
        output.Append(" --docker-sandbox false");
        output.Append(" --docker-image ");
        output.Append(Quote(opt.DockerImage));

        if (!string.IsNullOrWhiteSpace(opt.Repo))
        {
            output.Append(" --repo ");
            output.Append(Quote(opt.Repo));
        }

        if (!string.IsNullOrWhiteSpace(opt.CliPath))
        {
            output.Append(" --cli-path ");
            output.Append(Quote(opt.CliPath));
        }

        if (!string.IsNullOrWhiteSpace(opt.CliUrl))
        {
            output.Append(" --cli-url ");
            output.Append(Quote(opt.CliUrl));
        }

        return output.ToString();
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunDockerAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("docker", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            return (-1, string.Empty, "Failed to start Docker.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }

    private static string MapPathToContainer(string repoRoot, string hostPath, string description, List<DockerMount> mounts, bool allowOutsideRepo, bool readOnly)
    {
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            throw new InvalidOperationException($"{description} path is required for Docker sandbox.");
        }

        var fullRepoRoot = Path.GetFullPath(repoRoot);
        var fullPath = Path.IsPathRooted(hostPath)
            ? Path.GetFullPath(hostPath)
            : Path.GetFullPath(Path.Combine(fullRepoRoot, hostPath));

        if (IsUnderPath(fullRepoRoot, fullPath))
        {
            var relative = Path.GetRelativePath(fullRepoRoot, fullPath);
            return NormalizeContainerPath(Path.Combine("/repo", relative));
        }

        if (!allowOutsideRepo)
        {
            throw new InvalidOperationException($"{description} must be within {fullRepoRoot} when using Docker sandbox.");
        }

        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new InvalidOperationException($"Unable to resolve directory for {description}.");
        }

        var containerDirectory = "/coralph-bin";
        if (!mounts.Any(m => string.Equals(m.HostPath, parentDirectory, StringComparison.Ordinal)))
        {
            mounts.Add(new DockerMount(parentDirectory, containerDirectory, readOnly));
        }

        return NormalizeContainerPath(Path.Combine(containerDirectory, Path.GetFileName(fullPath)));
    }

    private static bool IsUnderPath(string rootPath, string fullPath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var normalizedPath = Path.GetFullPath(fullPath);
        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || string.Equals(normalizedPath, normalizedRoot, StringComparison.Ordinal);
    }

    private static string NormalizeContainerPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        if (value.IndexOfAny([' ', '"']) >= 0)
            return $"\"{value.Replace("\"", "\\\"")}\"";
        return value;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete temporary Docker sandbox file {Path}", path);
        }
    }

    private static string CombineOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return stderr ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(stderr))
        {
            return stdout;
        }

        return $"{stdout}{Environment.NewLine}{stderr}";
    }

    private static void WriteDockerOutput(string stdout, string stderr)
    {
        if (!string.IsNullOrEmpty(stdout))
        {
            ConsoleOutput.Write(stdout);
        }

        if (!string.IsNullOrEmpty(stderr))
        {
            ConsoleOutput.WriteError(stderr);
        }
    }

    private static string? ResolveManagedAssembly(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        if (location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(location))
        {
            return location;
        }

        var directory = Path.GetDirectoryName(location);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var candidate = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(location)}.dll");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? ResolveAssemblyFromBaseDirectory()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is null)
        {
            return null;
        }

        var name = entryAssembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var candidate = Path.Combine(AppContext.BaseDirectory, $"{name}.dll");
        return File.Exists(candidate) ? candidate : null;
    }

    private static DockerLaunchInfo BuildDotnetLaunch(string repoRoot, string assemblyPath)
    {
        var mounts = new List<DockerMount>();
        var containerPath = MapPathToContainer(repoRoot, assemblyPath, "Coralph assembly", mounts, allowOutsideRepo: true, readOnly: true);
        return new DockerLaunchInfo("dotnet", new List<string> { containerPath }, mounts);
    }

    private static DockerLaunchInfo BuildBinaryLaunch(string repoRoot, string binaryPath)
    {
        var mounts = new List<DockerMount>();
        var containerPath = MapPathToContainer(repoRoot, binaryPath, "Coralph executable", mounts, allowOutsideRepo: true, readOnly: true);
        return new DockerLaunchInfo(containerPath, new List<string>(), mounts);
    }
}
