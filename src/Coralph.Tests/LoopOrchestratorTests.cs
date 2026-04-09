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

    [Fact]
    public void IsTerminalSignalStillValid_WithCompleteAndRemainingTasks_ReturnsFalse()
    {
        var state = new LoopIterationState(
            IssuesJson: """
                [
                  { "number": 1, "title": "Open", "state": "open" }
                ]
                """,
            ProgressText: string.Empty,
            GeneratedTasksJson: """
                {
                  "version": 1,
                  "tasks": [
                    { "id": "1-001", "issueNumber": 1, "title": "Work", "status": "open", "order": 1 }
                  ]
                }
                """,
            GitHead: "abc",
            GitStatus: string.Empty);

        var isValid = LoopOrchestrator.IsTerminalSignalStillValid(TerminalSignal.Complete, state);

        Assert.False(isValid);
    }
}
