using System.Diagnostics;
using System.Text;
using Coralph;

namespace Coralph.Tests;

public sealed class DockerSandboxArgumentTests : IDisposable
{
    private readonly string _repoRoot;
    private readonly string _combinedPromptPath;

    public DockerSandboxArgumentTests()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"coralph-docker-sandbox-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
        _combinedPromptPath = Path.Combine(_repoRoot, "combined-prompt.txt");
    }

    [Fact]
    public void BuildDockerRunProcessStartInfo_WithProviderApiKey_UsesEnvironmentCopy()
    {
        var psi = BuildProcessStartInfo(new LoopOptions { ProviderApiKey = "sk-test-secret" });

        var args = psi.ArgumentList.ToArray();
        Assert.DoesNotContain("--provider-api-key", args);
        Assert.DoesNotContain("sk-test-secret", args);
        Assert.Contains("-e", args);
        Assert.Contains("CORALPH_PROVIDER_API_KEY", args);
        Assert.Equal("sk-test-secret", psi.Environment["CORALPH_PROVIDER_API_KEY"]);
    }

    [Fact]
    public void BuildDockerRunProcessStartInfo_WithoutProviderApiKey_DoesNotAddProviderCopy()
    {
        var psi = BuildProcessStartInfo(new LoopOptions());

        var args = psi.ArgumentList.ToArray();
        Assert.DoesNotContain("--provider-api-key", args);
        Assert.DoesNotContain("CORALPH_PROVIDER_API_KEY", args);
    }

    [Fact]
    public void BuildDockerRunProcessStartInfo_AddsDockerHardeningDefaults()
    {
        var psi = BuildProcessStartInfo(new LoopOptions());

        var args = psi.ArgumentList.ToArray();
        AssertArgumentPair(args, "--network", "none");
        AssertArgumentPair(args, "--security-opt", "no-new-privileges");
        AssertArgumentPair(args, "--memory", "2g");
        AssertArgumentPair(args, "--cpus", "2");
    }

    [Fact]
    public void BuildDockerRunProcessStartInfo_UsesConfiguredDockerLimits()
    {
        var psi = BuildProcessStartInfo(new LoopOptions
        {
            DockerNetworkMode = "bridge",
            DockerMemoryLimit = "4g",
            DockerCpuLimit = "1.5"
        });

        var args = psi.ArgumentList.ToArray();
        AssertArgumentPair(args, "--network", "bridge");
        AssertArgumentPair(args, "--memory", "4g");
        AssertArgumentPair(args, "--cpus", "1.5");
    }

    [Fact]
    public void BuildDockerRunProcessStartInfo_WithCopilotConfigPath_MountsReadOnly()
    {
        var configPath = Path.Combine(_repoRoot, ".copilot-config");
        Directory.CreateDirectory(configPath);

        var psi = BuildProcessStartInfo(new LoopOptions { CopilotConfigPath = configPath });

        var args = psi.ArgumentList.ToArray();
        Assert.Contains($"{configPath}:/home/vscode/.copilot:ro", args);
        Assert.Contains($"{configPath}:/root/.copilot:ro", args);
    }

    [Fact]
    public void BuildDockerRunProcessStartInfo_WithInvalidDockerImage_Throws()
    {
        var options = new LoopOptions { DockerImage = "coralph:latest;rm -rf /" };

        var ex = Assert.Throws<InvalidOperationException>(() => BuildProcessStartInfo(options));

        Assert.Contains("Invalid Docker image", ex.Message);
    }

    [Fact]
    public void AppendStreamedOutput_CapsTailAndPreservesTerminalSignal()
    {
        var buffer = new StringBuilder();
        DockerSandbox.AppendStreamedOutput(buffer, new string('x', DockerSandbox.StreamedOutputTailLimit + 2048), capTail: true);
        DockerSandbox.AppendStreamedOutput(buffer, "\nCOMPLETE\n", capTail: true);

        Assert.True(buffer.Length <= DockerSandbox.StreamedOutputTailLimit);
        Assert.True(PromptHelpers.TryGetTerminalSignal(buffer.ToString(), out var signal));
        Assert.Equal(TerminalSignal.Complete, signal);
    }

    private ProcessStartInfo BuildProcessStartInfo(LoopOptions options)
    {
        var launchInfo = new DockerLaunchInfo("dotnet", ["Coralph.dll"], []);
        return DockerSandbox.BuildDockerRunProcessStartInfo(options, _repoRoot, _combinedPromptPath, launchInfo);
    }

    private static void AssertArgumentPair(string[] args, string name, string value)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && args[i + 1] == value)
            {
                return;
            }
        }

        Assert.Fail($"Expected argument pair '{name} {value}' not found.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
    }
}
