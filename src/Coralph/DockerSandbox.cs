using System.Buffers;
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

    private static readonly SearchValues<char> QuoteChars = SearchValues.Create([' ', '"']);

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

    internal static async Task<DockerCheckResult> CheckCopilotCliAsync(string dockerImage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dockerImage))
        {
            return new DockerCheckResult(false, "Docker image is required for sandboxed Copilot runs.");
        }

        var args = new StringBuilder();
        args.Append("run --rm --pull=missing --entrypoint ");
        args.Append(Quote("sh"));
        args.Append(' ');
        args.Append(Quote(dockerImage));
        args.Append(" -lc ");
        args.Append(Quote("copilot --version"));

        var result = await RunDockerAsync(args.ToString(), ct);
        if (result.ExitCode == 0)
        {
            return new DockerCheckResult(true, null);
        }

        var details = string.IsNullOrWhiteSpace(result.Error) ? result.Output.Trim() : result.Error.Trim();
        var message = "GitHub Copilot CLI was not found or could not start inside the Docker image. " +
                      "Install it in the image (Node.js 24+ and `npm i -g @github/copilot`), " +
                      "or pass --cli-path to a CLI available in the container, or --cli-url to an existing Copilot CLI server.";
        if (!string.IsNullOrWhiteSpace(details))
        {
            message = $"{message}{Environment.NewLine}{details}";
        }

        return new DockerCheckResult(false, message);
    }

    internal static async Task<string> RunIterationAsync(LoopOptions opt, string combinedPrompt, int iteration, CancellationToken ct)
    {
        var repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var combinedPromptPath = Path.Combine(repoRoot, $".coralph-combined-prompt-{iteration}-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(combinedPromptPath, combinedPrompt, ct);

        try
        {
            var launchInfo = ResolveLaunchInfo(repoRoot);
            var args = BuildDockerRunArguments(opt, repoRoot, combinedPromptPath, launchInfo);
            var result = await RunDockerAsync(args, ct, streamOutput: true);
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

    private static string BuildDockerRunArguments(LoopOptions opt, string repoRoot, string combinedPromptPath, DockerLaunchInfo launchInfo)
    {
        if (!string.IsNullOrWhiteSpace(opt.CopilotConfigPath))
        {
            AddCopilotConfigMounts(launchInfo.Mounts, opt.CopilotConfigPath);
        }

        var combinedPromptContainerPath = MapPathToContainer(repoRoot, combinedPromptPath, "Combined prompt file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: true);
        var promptContainerPath = MapPathToContainer(repoRoot, opt.PromptFile, "Prompt file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: true);
        var progressContainerPath = MapPathToContainer(repoRoot, opt.ProgressFile, "Progress file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: false);
        var issuesContainerPath = MapPathToContainer(repoRoot, opt.IssuesFile, "Issues file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: true);
        var generatedTasksContainerPath = MapPathToContainer(repoRoot, opt.GeneratedTasksFile, "Generated tasks file", launchInfo.Mounts, allowOutsideRepo: false, readOnly: false);
        string? cliContainerPath = null;
        if (!string.IsNullOrWhiteSpace(opt.CliPath))
        {
            cliContainerPath = MapPathToContainer(repoRoot, opt.CliPath, "Copilot CLI", launchInfo.Mounts, allowOutsideRepo: true, readOnly: true);
        }

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
        output.Append("DOTNET_ROLL_FORWARD_TO_PRERELEASE=1");
        output.Append(" -e ");
        output.Append(Quote($"{CombinedPromptEnv}={combinedPromptContainerPath}"));
        AddCopilotTokenEnvironment(output, opt);

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
        output.Append(Quote(promptContainerPath));
        output.Append(" --progress-file ");
        output.Append(Quote(progressContainerPath));
        output.Append(" --issues-file ");
        output.Append(Quote(issuesContainerPath));
        output.Append(" --generated-tasks-file ");
        output.Append(Quote(generatedTasksContainerPath));
        output.Append(" --model ");
        output.Append(Quote(opt.Model));
        output.Append(" --show-reasoning ");
        output.Append(opt.ShowReasoning.ToString().ToLowerInvariant());
        output.Append(" --colorized-output ");
        output.Append(opt.ColorizedOutput.ToString().ToLowerInvariant());
        output.Append(" --stream-events false");
        output.Append(" --docker-sandbox false");
        output.Append(" --docker-image ");
        output.Append(Quote(opt.DockerImage));

        if (!string.IsNullOrWhiteSpace(opt.Repo))
        {
            output.Append(" --repo ");
            output.Append(Quote(opt.Repo));
        }

        if (!string.IsNullOrWhiteSpace(cliContainerPath))
        {
            output.Append(" --cli-path ");
            output.Append(Quote(cliContainerPath));
        }

        if (!string.IsNullOrWhiteSpace(opt.CliUrl))
        {
            output.Append(" --cli-url ");
            output.Append(Quote(opt.CliUrl));
        }

        if (!string.IsNullOrWhiteSpace(opt.ProviderType))
        {
            output.Append(" --provider-type ");
            output.Append(Quote(opt.ProviderType));
        }

        if (!string.IsNullOrWhiteSpace(opt.ProviderBaseUrl))
        {
            output.Append(" --provider-base-url ");
            output.Append(Quote(opt.ProviderBaseUrl));
        }

        if (!string.IsNullOrWhiteSpace(opt.ProviderWireApi))
        {
            output.Append(" --provider-wire-api ");
            output.Append(Quote(opt.ProviderWireApi));
        }

        if (!string.IsNullOrWhiteSpace(opt.ProviderApiKey))
        {
            output.Append(" --provider-api-key ");
            output.Append(Quote(opt.ProviderApiKey));
        }

        return output.ToString();
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunDockerAsync(string arguments, CancellationToken ct, bool streamOutput = false)
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

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var stdoutTask = ReadProcessStreamAsync(process.StandardOutput, stdout, streamOutput ? ConsoleOutput.Write : null, ct);
        var stderrTask = ReadProcessStreamAsync(process.StandardError, stderr, streamOutput ? ConsoleOutput.WriteError : null, ct);

        await process.WaitForExitAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static async Task ReadProcessStreamAsync(StreamReader reader, StringBuilder buffer, Action<string>? write, CancellationToken ct)
    {
        var chunk = new char[4096];
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await reader.ReadAsync(chunk.AsMemory(0, chunk.Length), ct);
            if (read == 0)
            {
                break;
            }

            var text = new string(chunk, 0, read);
            buffer.Append(text);
            write?.Invoke(text);
        }
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

        var existingMount = mounts.FirstOrDefault(m => string.Equals(m.HostPath, parentDirectory, StringComparison.Ordinal));
        var containerDirectory = existingMount?.ContainerPath;
        if (string.IsNullOrWhiteSpace(containerDirectory))
        {
            containerDirectory = "/coralph-bin";
            if (mounts.Any(m => string.Equals(m.ContainerPath, containerDirectory, StringComparison.Ordinal)))
            {
                var suffix = 2;
                while (mounts.Any(m => string.Equals(m.ContainerPath, $"{containerDirectory}-{suffix}", StringComparison.Ordinal)))
                {
                    suffix++;
                }
                containerDirectory = $"{containerDirectory}-{suffix}";
            }
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
        if (value.AsSpan().IndexOfAny(QuoteChars) >= 0)
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

    private static void AddCopilotConfigMounts(List<DockerMount> mounts, string hostPath)
    {
        var fullPath = Path.GetFullPath(hostPath);
        if (!Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Copilot config directory not found: {fullPath}");
        }

        AddMountIfMissing(mounts, fullPath, "/home/vscode/.copilot", readOnly: false);
        AddMountIfMissing(mounts, fullPath, "/root/.copilot", readOnly: false);
    }

    private static void AddMountIfMissing(List<DockerMount> mounts, string hostPath, string containerPath, bool readOnly)
    {
        foreach (var mount in mounts)
        {
            if (string.Equals(mount.HostPath, hostPath, StringComparison.Ordinal) &&
                string.Equals(mount.ContainerPath, containerPath, StringComparison.Ordinal))
            {
                return;
            }
        }

        mounts.Add(new DockerMount(hostPath, containerPath, readOnly));
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

    private static void AddCopilotTokenEnvironment(StringBuilder output, LoopOptions opt)
    {
        if (!string.IsNullOrWhiteSpace(opt.CopilotToken))
        {
            AddDockerEnv(output, "GH_TOKEN", opt.CopilotToken);
            return;
        }

        var ghToken = Environment.GetEnvironmentVariable("GH_TOKEN");
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        if (!string.IsNullOrWhiteSpace(ghToken))
        {
            AddDockerEnv(output, "GH_TOKEN", ghToken);
        }

        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            AddDockerEnv(output, "GITHUB_TOKEN", githubToken);
        }
    }

    private static void AddDockerEnv(StringBuilder output, string name, string value)
    {
        output.Append(" -e ");
        output.Append(Quote($"{name}={value}"));
    }
}
