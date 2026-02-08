using System.Text.Json;
using Coralph;

namespace Coralph.Tests;

public class TaskBacklogTests
{
    [Fact]
    public void BuildBacklogJson_WithChecklistIssue_SplitsChecklistItemsIntoTasks()
    {
        var issuesJson = """
            [
              {
                "number": 42,
                "title": "PRD: Checkout redesign",
                "body": "- [ ] Add API endpoint for totals\n- [x] Draft UX flow for checkout",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks");

        Assert.Equal(2, tasks.GetArrayLength());
        Assert.Equal("Add API endpoint for totals", tasks[0].GetProperty("title").GetString());
        Assert.Equal("open", tasks[0].GetProperty("status").GetString());
        Assert.Equal("done", tasks[1].GetProperty("status").GetString());
    }

    [Fact]
    public void BuildBacklogJson_WithPrdHeadings_CreatesTasksFromUsefulSections()
    {
        var issuesJson = """
            [
              {
                "number": 101,
                "title": "PRD: Analytics Dashboard",
                "body": "## Overview\nGeneral context.\n\n## API Contract\nDefine backend response shape for dashboard metrics.\n\n## Frontend Rendering\nRender charts and table with loading + error states.",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks");
        var titles = tasks.EnumerateArray().Select(t => t.GetProperty("title").GetString()).ToArray();

        Assert.Contains("API Contract", titles);
        Assert.Contains("Frontend Rendering", titles);
        Assert.DoesNotContain("Overview", titles);
    }

    [Fact]
    public void BuildBacklogJson_WithHeadingsListsAndComments_PrefersListItems()
    {
        var issuesJson = """
            [
              {
                "number": 67,
                "title": "Modernization work",
                "body": "### Problem statement\nContext.\n\n### Proposed solution\n1. Add unit tests for TaskBacklog.cs\n2. Migrate to GeneratedRegex\n\n### Alternatives considered\nLegacy.",
                "comments": [
                  { "body": "Remaining work:\n- Add PermissionPolicy tests\n- Add ConfigureAwait(false) to async code" }
                ],
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();
        var titles = tasks.Select(t => t.GetProperty("title").GetString()).ToArray();

        Assert.Contains("Add unit tests for TaskBacklog.cs", titles);
        Assert.Contains("Migrate to GeneratedRegex", titles);
        Assert.Contains("Add PermissionPolicy tests", titles);
        Assert.Contains("Add ConfigureAwait(false) to async code", titles);

        var commentTask = tasks.First(t => t.GetProperty("title").GetString() == "Add PermissionPolicy tests");
        Assert.Equal("comment", commentTask.GetProperty("origin").GetString());
    }

    [Fact]
    public void BuildBacklogJson_WithExistingBacklog_PreservesTaskStatus()
    {
        var issuesJson = """
            [
              {
                "number": 7,
                "title": "PRD: Notifications",
                "body": "1. Create event model\n2. Add notification endpoint\n3. Add worker",
                "state": "open"
              }
            ]
            """;

        var initialBacklogJson = TaskBacklog.BuildBacklogJson(issuesJson);
        var manuallyUpdatedBacklogJson = initialBacklogJson.Replace("\"status\": \"open\"", "\"status\": \"done\"", StringComparison.Ordinal);

        var rebuiltBacklogJson = TaskBacklog.BuildBacklogJson(issuesJson, manuallyUpdatedBacklogJson);

        using var doc = JsonDocument.Parse(rebuiltBacklogJson);
        var firstTaskStatus = doc.RootElement.GetProperty("tasks")[0].GetProperty("status").GetString();

        Assert.Equal("done", firstTaskStatus);
    }

    [Fact]
    public void BuildBacklogJson_WithSimpleIssue_FallsBackToSingleTask()
    {
        var issuesJson = """
            [
              {
                "number": 5,
                "title": "Fix null reference in parser",
                "body": "Null check is missing in ParseConfig()",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks");

        Assert.Equal(1, tasks.GetArrayLength());
        Assert.Equal("Fix null reference in parser", tasks[0].GetProperty("title").GetString());
    }

    [Fact]
    public void BuildBacklogJson_WithLargePrdAndShortChecklist_MergesChecklistAndHeadingTasks()
    {
        var sections = string.Join("\n\n", Enumerable.Range(1, 10).Select(i =>
            $"## Feature Slice {i}\n- Define API contract\n- Implement service behavior\n- Add tests"));

        var filler = string.Join("\n", Enumerable.Repeat(
            "Additional detail paragraph for non-functional requirements and rollout strategy.", 70));

        var body = """
            - [ ] Build onboarding tracer bullet
            - [ ] Add observability dashboards

            """ + sections + "\n\n" + filler;

        var issuesJson = $$"""
            [
              {
                "number": 808,
                "title": "PRD: Reliability and onboarding",
                "body": {{JsonSerializer.Serialize(body)}},
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.True(tasks.Length >= 8);
        Assert.Contains(tasks, task => task.GetProperty("origin").GetString() == "checklist");
        Assert.Contains(tasks, task => task.GetProperty("origin").GetString() == "heading");
        Assert.Contains(tasks, task => task.GetProperty("title").GetString() == "Build onboarding tracer bullet");
        Assert.Contains(tasks, task => task.GetProperty("title").GetString() == "Feature Slice 1");
    }
}
