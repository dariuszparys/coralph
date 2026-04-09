using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace Coralph;

internal record DockerCheckResult(bool Success, string? Message);
internal record DockerMount(string HostPath, string ContainerPath, bool ReadOnly);
internal record DockerLaunchInfo(string Command, List<string> Arguments, List<DockerMount> Mounts);

internal static class DockerSandbox
{
    internal const string SandboxFlagEnv = "CORALPH_DOCKER_SANDBOX";
    internal const string CombinedPromptEnv = "CORALPH_COMBINED_PROMPT_FILE";
    internal const int StreamedOutputTailLimit = 16 * 1024;

    private static readonly SearchValues<char> QuoteChars = SearchValues.Create([' ', '"']);
    private static readonly Regex DockerImageValidationRegex = new("^[A-Za-z0-9._/:-]+$", RegexOptions.Compiled);

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
        if (!TryValidateDockerImage(dockerImage, out var normalizedDockerImage, out var imageError))
        {
            return new DockerCheckResult(false, imageError);
        }

        var args = new StringBuilder();
        args.Append("run --rm --pull=missing --entrypoint ");
        args.Append(Quote("sh"));
        args.Append(' ');
        args.Append(Quote(normalizedDockerImage));
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
            var psi = BuildDockerRunProcessStartInfo(opt, repoRoot, combinedPromptPath, launchInfo);
            var result = await RunDockerAsync(psi, ct, streamOutput: true);
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

    internal static ProcessStartInfo BuildDockerRunProcessStartInfo(LoopOptions opt, string repoRoot, string combinedPromptPath, DockerLaunchInfo launchInfo)
    {
        var dockerImage = NormalizeDockerImage(opt.DockerImage);
        var psi = CreateDockerProcessStartInfo();

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

        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--rm");
        psi.ArgumentList.Add("--pull=missing");
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add($"{repoRoot}:/repo");
        psi.ArgumentList.Add("-w");
        psi.ArgumentList.Add("/repo");
        foreach (var mount in launchInfo.Mounts)
        {
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add($"{mount.HostPath}:{mount.ContainerPath}{(mount.ReadOnly ? ":ro" : string.Empty)}");
        }
        psi.ArgumentList.Add("--network");
        psi.ArgumentList.Add(NormalizeDockerRunValue(opt.DockerNetworkMode, "none"));
        psi.ArgumentList.Add("--security-opt");
        psi.ArgumentList.Add("no-new-privileges");
        psi.ArgumentList.Add("--memory");
        psi.ArgumentList.Add(NormalizeDockerRunValue(opt.DockerMemoryLimit, "2g"));
        psi.ArgumentList.Add("--cpus");
        psi.ArgumentList.Add(NormalizeDockerRunValue(opt.DockerCpuLimit, "2"));
        AddDockerEnv(psi, SandboxFlagEnv, "1");
        AddDockerEnv(psi, "DOTNET_ROLL_FORWARD_TO_PRERELEASE", "1");
        AddDockerEnv(psi, CombinedPromptEnv, combinedPromptContainerPath);
        AddCopilotTokenEnvironment(psi, opt);
        if (!string.IsNullOrWhiteSpace(opt.ProviderApiKey))
        {
            AddDockerEnv(psi, "CORALPH_PROVIDER_API_KEY", opt.ProviderApiKey);
        }

        psi.ArgumentList.Add(dockerImage);
        psi.ArgumentList.Add(launchInfo.Command);
        foreach (var argument in launchInfo.Arguments)
        {
            psi.ArgumentList.Add(argument);
        }
        psi.ArgumentList.Add("--max-iterations");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("--prompt-file");
        psi.ArgumentList.Add(promptContainerPath);
        psi.ArgumentList.Add("--progress-file");
        psi.ArgumentList.Add(progressContainerPath);
        psi.ArgumentList.Add("--issues-file");
        psi.ArgumentList.Add(issuesContainerPath);
        psi.ArgumentList.Add("--generated-tasks-file");
        psi.ArgumentList.Add(generatedTasksContainerPath);
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(opt.Model);
        psi.ArgumentList.Add("--show-reasoning");
        psi.ArgumentList.Add(opt.ShowReasoning.ToString().ToLowerInvariant());
        psi.ArgumentList.Add("--colorized-output");
        psi.ArgumentList.Add(opt.ColorizedOutput.ToString().ToLowerInvariant());
        psi.ArgumentList.Add("--ui");
        psi.ArgumentList.Add("classic");
        psi.ArgumentList.Add("--stream-events");
        psi.ArgumentList.Add("false");
        psi.ArgumentList.Add("--docker-sandbox");
        psi.ArgumentList.Add("false");
        psi.ArgumentList.Add("--docker-image");
        psi.ArgumentList.Add(dockerImage);

        if (!string.IsNullOrWhiteSpace(opt.Repo))
        {
            psi.ArgumentList.Add("--repo");
            psi.ArgumentList.Add(opt.Repo);
        }

        if (!string.IsNullOrWhiteSpace(cliContainerPath))
        {
            psi.ArgumentList.Add("--cli-path");
            psi.ArgumentList.Add(cliContainerPath);
        }

        if (!string.IsNullOrWhiteSpace(opt.CliUrl))
        {
            psi.ArgumentList.Add("--cli-url");
            psi.ArgumentList.Add(opt.CliUrl);
        }

        if (!string.IsNullOrWhiteSpace(opt.ProviderType))
        {
            psi.ArgumentList.Add("--provider-type");
            psi.ArgumentList.Add(opt.ProviderType);
        }

        if (!string.IsNullOrWhiteSpace(opt.ProviderBaseUrl))
        {
            psi.ArgumentList.Add("--provider-base-url");
            psi.ArgumentList.Add(opt.ProviderBaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(opt.ProviderWireApi))
        {
            psi.ArgumentList.Add("--provider-wire-api");
            psi.ArgumentList.Add(opt.ProviderWireApi);
        }

        return psi;
    }

    private static ProcessStartInfo CreateDockerProcessStartInfo()
    {
        return new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunDockerAsync(ProcessStartInfo psi, CancellationToken ct, bool streamOutput = false)
    {
        using var process = Process.Start(psi);
        if (process is null)
            return (-1, string.Empty, "Failed to start Docker.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var stdoutTask = ReadProcessStreamAsync(process.StandardOutput, stdout, streamOutput ? ConsoleOutput.Write : null, ct);
        var stderrTask = ReadProcessStreamAsync(process.StandardError, stderr, streamOutput ? ConsoleOutput.WriteError : null, ct);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await TryTerminateProcessTreeAsync(process).ConfigureAwait(false);
            throw;
        }
        finally
        {
            try
            {
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Expected when the caller cancels process streaming.
            }
        }

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
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

        return await RunDockerAsync(psi, ct, streamOutput).ConfigureAwait(false);
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
            AppendStreamedOutput(buffer, text, write is not null);
            write?.Invoke(text);
        }
    }

    private static async Task TryTerminateProcessTreeAsync(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to query Docker process exit state during cancellation");
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to terminate Docker process tree after cancellation");
            return;
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Log.Warning("Timed out waiting for Docker process tree to terminate after cancellation");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed while waiting for Docker process termination after cancellation");
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

        AddMountIfMissing(mounts, fullPath, "/home/vscode/.copilot", readOnly: true);
        AddMountIfMissing(mounts, fullPath, "/root/.copilot", readOnly: true);
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

    internal static void AppendStreamedOutput(StringBuilder buffer, string text, bool capTail)
    {
        buffer.Append(text);
        if (capTail && buffer.Length > StreamedOutputTailLimit)
        {
            buffer.Remove(0, buffer.Length - StreamedOutputTailLimit);
        }
    }

    private static string NormalizeDockerImage(string dockerImage)
    {
        if (!TryValidateDockerImage(dockerImage, out var normalized, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return normalized;
    }

    private static bool TryValidateDockerImage(string dockerImage, out string normalizedDockerImage, out string error)
    {
        normalizedDockerImage = dockerImage?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedDockerImage))
        {
            error = "Docker image is required for sandboxed Copilot runs.";
            return false;
        }

        if (!DockerImageValidationRegex.IsMatch(normalizedDockerImage))
        {
            error = $"Invalid Docker image '{normalizedDockerImage}'. Only letters, numbers, '.', '_', '/', ':', and '-' are allowed.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeDockerRunValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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

    private static void AddCopilotTokenEnvironment(ProcessStartInfo psi, LoopOptions opt)
    {
        if (!string.IsNullOrWhiteSpace(opt.CopilotToken))
        {
            AddDockerEnv(psi, "GH_TOKEN", opt.CopilotToken);
            return;
        }

        var ghToken = Environment.GetEnvironmentVariable("GH_TOKEN");
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        if (!string.IsNullOrWhiteSpace(ghToken))
        {
            AddDockerEnv(psi, "GH_TOKEN", ghToken);
        }

        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            AddDockerEnv(psi, "GITHUB_TOKEN", githubToken);
        }
    }

    private static void AddDockerEnv(ProcessStartInfo psi, string name, string value)
    {
        psi.Environment[name] = value;
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(name);
    }
}
