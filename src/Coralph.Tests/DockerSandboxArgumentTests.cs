using System.Diagnostics;
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

    private ProcessStartInfo BuildProcessStartInfo(LoopOptions options)
    {
        var launchInfo = new DockerLaunchInfo("dotnet", ["Coralph.dll"], []);
        return DockerSandbox.BuildDockerRunProcessStartInfo(options, _repoRoot, _combinedPromptPath, launchInfo);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
    }
}
