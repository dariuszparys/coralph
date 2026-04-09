using Coralph;

namespace Coralph.Tests;

public sealed class LoopIterationStateTests
{
    [Fact]
    public void TryGetImplicitTerminalSignal_WhenIssuesAreClosed_ReturnsNoOpenIssues()
    {
        var state = new LoopIterationState(
            IssuesJson: """
                [
                  { "number": 1, "title": "Done", "state": "closed" }
                ]
                """,
            ProgressText: string.Empty,
            GeneratedTasksJson: """
                { "version": 1, "tasks": [] }
                """,
            GitHead: "abc",
            GitStatus: string.Empty);

        var hasSignal = state.TryGetImplicitTerminalSignal(out var signal, out var error);

        Assert.True(hasSignal);
        Assert.Null(error);
        Assert.Equal(TerminalSignal.NoOpenIssues, signal);
    }

    [Fact]
    public void TryGetImplicitTerminalSignal_WhenBacklogHasNoRemainingTasks_ReturnsAllTasksComplete()
    {
        var state = new LoopIterationState(
            IssuesJson: """
                [
                  { "number": 1, "title": "Still open", "state": "open" }
                ]
                """,
            ProgressText: string.Empty,
            GeneratedTasksJson: """
                {
                  "version": 1,
                  "tasks": [
                    { "id": "1-001", "issueNumber": 1, "title": "Done", "status": "done", "order": 1 }
                  ]
                }
                """,
            GitHead: "abc",
            GitStatus: string.Empty);

        var hasSignal = state.TryGetImplicitTerminalSignal(out var signal, out var error);

        Assert.True(hasSignal);
        Assert.Null(error);
        Assert.Equal(TerminalSignal.AllTasksComplete, signal);
    }

    [Fact]
    public void HasMeaningfulChangesComparedTo_DetectsArtifactAndGitChanges()
    {
        var before = new LoopIterationState("[]", "", "", "head-a", "");
        var after = new LoopIterationState("[]", "", "", "head-b", " M src/Coralph/LoopOrchestrator.cs");

        Assert.True(after.HasMeaningfulChangesComparedTo(before));
        Assert.False(before.HasMeaningfulChangesComparedTo(before));
    }
}
