using Coralph;

namespace Coralph.Tests;

public sealed class LoopOrchestratorTests
{
    [Fact]
    public void ResolveDockerExecution_WithSandboxDisabled_UsesHostWithoutFallback()
    {
        var decision = LoopOrchestrator.ResolveDockerExecution(dockerSandboxRequested: false, inDockerSandbox: false, unavailableReason: "Docker is not installed.");

        Assert.False(decision.UseDockerPerIteration);
        Assert.Null(decision.FallbackReason);
    }

    [Fact]
    public void ResolveDockerExecution_WithHealthySandbox_UsesDocker()
    {
        var decision = LoopOrchestrator.ResolveDockerExecution(dockerSandboxRequested: true, inDockerSandbox: false, unavailableReason: null);

        Assert.True(decision.UseDockerPerIteration);
        Assert.Null(decision.FallbackReason);
    }

    [Fact]
    public void ResolveDockerExecution_WithUnavailableSandbox_FallsBackToHost()
    {
        var decision = LoopOrchestrator.ResolveDockerExecution(dockerSandboxRequested: true, inDockerSandbox: false, unavailableReason: "Docker is not installed.");

        Assert.False(decision.UseDockerPerIteration);
        Assert.Equal("Docker is not installed.", decision.FallbackReason);
    }
}
