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
                "title": "PRD: Modernization work",
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
    public void BuildBacklogJson_WithNonPrdListOnlyIssue_FallsBackToSingleTask()
    {
        var issuesJson = """
            [
              {
                "number": 76,
                "title": "bug: init fails in standalone mode",
                "body": "### Steps to reproduce\n1. Build the binary\n2. Run coralph --init\n\n### Expected behavior\n- It should succeed",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Single(tasks);
        Assert.Equal("bug: init fails in standalone mode", tasks[0].GetProperty("title").GetString());
        Assert.Equal("fallback", tasks[0].GetProperty("origin").GetString());
    }

    [Fact]
    public void BuildBacklogJson_WithTaskModeOverridePrd_UsesPrdFanout()
    {
        var issuesJson = """
            [
              {
                "number": 77,
                "title": "Feature backlog",
                "body": "<!-- coralph:task-mode=prd -->\n1. Build API layer for onboarding flow\n2. Add UI wiring for onboarding flow\n3. Add end-to-end integration tests for onboarding",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Equal(3, tasks.Length);
        Assert.Equal("Build API layer for onboarding flow", tasks[0].GetProperty("title").GetString());
    }

    [Fact]
    public void BuildBacklogJson_WithLargeNonPrdIssue_UsesModerateFanout()
    {
        var filler = string.Join('\n', Enumerable.Repeat(
            "Implementation details for resiliency, rollout safety, and service-level diagnostics across components.", 80));

        var body = """
            ### Steps to reproduce
            1. Build the binary
            2. Run the command

            ### Environment
            - OS: macOS
            - Runtime: .NET 10

            ### Implementation slices
            1. Implement the orchestration API for task selection and execution
            2. Add storage persistence for generated task transitions and retries
            3. Add guardrails for terminal signals and completion checks

            ### Validation plan
            1. Add integration tests for successful iteration and completion
            2. Add failure-path coverage for invalid signals and stale status transitions
            """ + "\n\n" + filler;

        var issuesJson = $$"""
            [
              {
                "number": 88,
                "title": "Feature: Orchestration hardening",
                "body": {{JsonSerializer.Serialize(body)}},
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();
        var titles = tasks.Select(t => t.GetProperty("title").GetString()!).ToArray();

        Assert.InRange(tasks.Length, 3, 8);
        Assert.DoesNotContain("Steps to reproduce", titles);
        Assert.DoesNotContain("Environment", titles);
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

    [Fact]
    public void BuildBacklogJson_WithNullOrEmptyIssuesJson_ReturnsEmptyBacklog()
    {
        var backlogJson1 = TaskBacklog.BuildBacklogJson(null!);
        var backlogJson2 = TaskBacklog.BuildBacklogJson(string.Empty);
        var backlogJson3 = TaskBacklog.BuildBacklogJson("   ");

        using var doc1 = JsonDocument.Parse(backlogJson1);
        using var doc2 = JsonDocument.Parse(backlogJson2);
        using var doc3 = JsonDocument.Parse(backlogJson3);

        Assert.Empty(doc1.RootElement.GetProperty("tasks").EnumerateArray());
        Assert.Empty(doc2.RootElement.GetProperty("tasks").EnumerateArray());
        Assert.Empty(doc3.RootElement.GetProperty("tasks").EnumerateArray());
    }

    [Fact]
    public void BuildBacklogJson_WithClosedIssues_SkipsClosedIssues()
    {
        var issuesJson = """
            [
              {
                "number": 1,
                "title": "Open Issue",
                "body": "Do something",
                "state": "open"
              },
              {
                "number": 2,
                "title": "Closed Issue",
                "body": "Already done",
                "state": "closed"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Single(tasks);
        Assert.Equal("Open Issue", tasks[0].GetProperty("issueTitle").GetString());
    }

    [Fact]
    public void BuildBacklogJson_WithInvalidJson_ThrowsJsonException()
    {
        var invalidJson = "{ this is not valid json }";

        // JsonReaderException is a subclass of JsonException
        var ex = Assert.ThrowsAny<JsonException>(() => TaskBacklog.BuildBacklogJson(invalidJson));
        Assert.NotNull(ex);
    }

    [Fact]
    public void BuildBacklogJson_WithNonArrayRootElement_ReturnsEmptyBacklog()
    {
        var issuesJson = """{ "issues": [] }""";

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        Assert.Empty(doc.RootElement.GetProperty("tasks").EnumerateArray());
    }

    [Fact]
    public void BuildBacklogJson_GeneratesStableKeysForTasks()
    {
        var issuesJson = """
            [
              {
                "number": 10,
                "title": "PRD: Test Issue",
                "body": "1. Add feature A\n2. Add feature B\n3. Add feature A again",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // Should have 3 tasks even though "Add feature A" appears twice
        Assert.Equal(3, tasks.Length);

        var stableKeys = tasks.Select(t => t.GetProperty("stableKey").GetString()).ToArray();
        // Stable keys should handle duplicates by appending counter
        Assert.Contains(stableKeys, k => k!.StartsWith("10:add-feature-a"));
        Assert.Contains(stableKeys, k => k!.StartsWith("10:add-feature-b"));
    }

    [Fact]
    public void BuildBacklogJson_HandlesMarkdownFormatting()
    {
        var issuesJson = """
            [
              {
                "number": 20,
                "title": "PRD: Test Issue",
                "body": "1. Add **bold** feature\n2. Add `code` snippet\n3. Add [link](https://example.com) text",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Equal(3, tasks.Length);
        Assert.Equal("Add bold feature", tasks[0].GetProperty("title").GetString());
        Assert.Equal("Add code snippet", tasks[1].GetProperty("title").GetString());
        Assert.Equal("Add link text", tasks[2].GetProperty("title").GetString());
    }

    [Fact]
    public void BuildBacklogJson_FiltersOutTooShortListItems_AndFallsBackToIssueTitle()
    {
        var issuesJson = """
            [
              {
                "number": 30,
                "title": "Test Issue",
                "body": "1. Short\n2. OK\n3. No",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // All list items are too short (< 12 chars), so fallback to single task with issue title
        Assert.Single(tasks);
        Assert.Equal("Test Issue", tasks[0].GetProperty("title").GetString());
        Assert.Equal("fallback", tasks[0].GetProperty("origin").GetString());
    }

    [Fact]
    public void BuildBacklogJson_AcceptsListItemsWithMinimumLength()
    {
        var issuesJson = """
            [
              {
                "number": 31,
                "title": "PRD: Test Issue",
                "body": "1. This task passes the minimum length requirement\n2. Another task that meets the minimum requirement\n3. And one more task that is long enough to pass",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // Need 3+ list items to use them without headings/long body
        Assert.Equal(3, tasks.Length);
        Assert.Contains("minimum length requirement", tasks[0].GetProperty("title").GetString());
        Assert.Contains("minimum requirement", tasks[1].GetProperty("title").GetString());
        Assert.Contains("long enough", tasks[2].GetProperty("title").GetString());
    }

    [Fact]
    public void BuildBacklogJson_FiltersOutMetadataLines_AndFallsBackIfAllFiltered()
    {
        var issuesJson = """
            [
              {
                "number": 40,
                "title": "Test Issue",
                "body": "1. Note: This is metadata\n2. Example: Sample code here\n3. https://github.com/example/repo",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // All list items are metadata, so fallback to single task
        Assert.Single(tasks);
        Assert.Equal("Test Issue", tasks[0].GetProperty("title").GetString());
        Assert.Equal("fallback", tasks[0].GetProperty("origin").GetString());
    }

    [Fact]
    public void BuildBacklogJson_AcceptsNonMetadataListItems()
    {
        var issuesJson = """
            [
              {
                "number": 41,
                "title": "PRD: Test Issue",
                "body": "1. Implement the feature properly and correctly\n2. Note: This is metadata\n3. Add comprehensive test coverage for everything\n4. Example: Sample code\n5. Deploy to production with monitoring enabled",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // Should have 3 tasks after filtering out 2 metadata lines
        Assert.Equal(3, tasks.Length);
        Assert.Contains("Implement the feature", tasks[0].GetProperty("title").GetString());
        Assert.Contains("test coverage", tasks[1].GetProperty("title").GetString());
        Assert.Contains("production", tasks[2].GetProperty("title").GetString());
    }

    [Fact]
    public void BuildBacklogJson_LimitsTasksPerIssue()
    {
        var items = string.Join('\n', Enumerable.Range(1, 50).Select(i => $"{i}. Valid task item number {i}"));
        var issuesJson = $$"""
            [
              {
                "number": 50,
                "title": "PRD: Large Issue",
                "body": {{JsonSerializer.Serialize(items)}},
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // Should be limited to MaxTasksPerIssue (25)
        Assert.Equal(25, tasks.Length);
    }

    [Fact]
    public void BuildBacklogJson_HandlesCommentsWithChecklistItems()
    {
        var issuesJson = """
            [
              {
                "number": 60,
                "title": "Test Issue",
                "body": "Main body",
                "comments": [
                  { "body": "- [ ] Task from comment A\n- [x] Task from comment B" }
                ],
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Equal(2, tasks.Length);
        Assert.Equal("Task from comment A", tasks[0].GetProperty("title").GetString());
        Assert.Equal("open", tasks[0].GetProperty("status").GetString());
        Assert.Equal("Task from comment B", tasks[1].GetProperty("title").GetString());
        Assert.Equal("done", tasks[1].GetProperty("status").GetString());
    }

    [Fact]
    public void BuildBacklogJson_PreservesIssueNumberAcrossRebuilds()
    {
        var issuesJson = """
            [
              {
                "number": 123,
                "title": "Test Issue",
                "body": "1. Task one\n2. Task two",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.All(tasks, task => Assert.Equal(123, task.GetProperty("issueNumber").GetInt32()));
    }

    [Fact]
    public void BuildBacklogJson_AssignsSequentialIdNumbers()
    {
        var issuesJson = """
            [
              {
                "number": 99,
                "title": "PRD: Test Issue",
                "body": "We need multiple tasks here:\n\n1. Implement task A with proper description\n2. Implement task B with proper description\n3. Implement task C with proper description",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.True(tasks.Length >= 3);
        Assert.Equal("99-001", tasks[0].GetProperty("id").GetString());
        Assert.Equal("99-002", tasks[1].GetProperty("id").GetString());
        Assert.Equal("99-003", tasks[2].GetProperty("id").GetString());
    }

    [Fact]
    public void BuildBacklogJson_TruncatesLongTitlesAndDescriptions()
    {
        var longText = new string('a', 300);
        var issuesJson = $$"""
            [
              {
                "number": 80,
                "title": "Test Issue",
                "body": "1. {{longText}}",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Single(tasks);
        var title = tasks[0].GetProperty("title").GetString()!;
        var description = tasks[0].GetProperty("description").GetString()!;

        // Title max = 140, description max = 320
        Assert.True(title.Length <= 140);
        Assert.True(description.Length <= 320);
    }

    [Fact]
    public void BuildBacklogJson_HandlesIssuesWithoutNumberProperty()
    {
        var issuesJson = """
            [
              {
                "title": "Issue without number",
                "body": "1. Task one",
                "state": "open"
              },
              {
                "title": "Another issue",
                "body": "1. Task two",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Equal(2, tasks.Length);
        // Should assign synthetic numbers (1, 2)
        Assert.Equal("1-001", tasks[0].GetProperty("id").GetString());
        Assert.Equal("2-001", tasks[1].GetProperty("id").GetString());
    }

    [Fact]
    public void BuildBacklogJson_DeduplicatesTasksWithSimilarTitles()
    {
        var issuesJson = """
            [
              {
                "number": 90,
                "title": "PRD: Test Issue",
                "body": "1. Add feature X implementation properly to the system\n2. Add Feature X implementation properly to the system\n3. ADD FEATURE X IMPLEMENTATION PROPERLY TO THE SYSTEM\n4. Add feature Y implementation properly to the system\n5. Add feature Z implementation properly to the system",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // Should dedupe the three variations of "Add feature X" -> 3 unique tasks
        Assert.Equal(3, tasks.Length);
        var titles = tasks.Select(t => t.GetProperty("title").GetString()!).ToArray();
        Assert.Single(titles, t => t.Contains("feature X", StringComparison.OrdinalIgnoreCase));
        Assert.Single(titles, t => t.Contains("feature Y", StringComparison.OrdinalIgnoreCase));
        Assert.Single(titles, t => t.Contains("feature Z", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildBacklogJson_NormalizesStatusValues()
    {
        var existingBacklog = """
            {
              "version": 1,
              "generatedAtUtc": "2024-01-01T00:00:00Z",
              "sourceIssueCount": 1,
              "tasks": [
                { "id": "100-001", "stableKey": "100:implement-the-complete-task-a", "issueNumber": 100, "issueTitle": "Test", "title": "Implement the complete Task A", "description": "A", "status": "completed", "origin": "list", "order": 1 },
                { "id": "100-002", "stableKey": "100:implement-the-complete-task-b", "issueNumber": 100, "issueTitle": "Test", "title": "Implement the complete Task B", "description": "B", "status": "in-progress", "origin": "list", "order": 2 },
                { "id": "100-003", "stableKey": "100:implement-the-complete-task-c", "issueNumber": 100, "issueTitle": "Test", "title": "Implement the complete Task C", "description": "C", "status": "blocked", "origin": "list", "order": 3 }
              ]
            }
            """;

        var issuesJson = """
            [
              {
                "number": 100,
                "title": "PRD: Test",
                "body": "We need:\n\n1. Implement the complete Task A\n2. Implement the complete Task B\n3. Implement the complete Task C",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson, existingBacklog);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        // "completed" should normalize to "done", "in-progress" to "in_progress"
        Assert.Equal("done", tasks[0].GetProperty("status").GetString());
        Assert.Equal("in_progress", tasks[1].GetProperty("status").GetString());
        Assert.Equal("blocked", tasks[2].GetProperty("status").GetString());
    }

    [Fact]
    public void HasOpenTasks_WithOpenTasks_ReturnsTrue()
    {
        var json = """
            {
              "version": 1,
              "tasks": [
                { "id": "1-001", "status": "open" },
                { "id": "1-002", "status": "done" }
              ]
            }
            """;

        Assert.True(TaskBacklog.HasOpenTasks(json));
    }

    [Fact]
    public void HasOpenTasks_AllDone_ReturnsFalse()
    {
        var json = """
            {
              "version": 1,
              "tasks": [
                { "id": "1-001", "status": "done" },
                { "id": "1-002", "status": "done" }
              ]
            }
            """;

        Assert.False(TaskBacklog.HasOpenTasks(json));
    }

    [Fact]
    public void HasOpenTasks_WithInProgressTasks_ReturnsTrue()
    {
        var json = """
            {
              "version": 1,
              "tasks": [
                { "id": "1-001", "status": "done" },
                { "id": "1-002", "status": "in_progress" }
              ]
            }
            """;

        Assert.True(TaskBacklog.HasOpenTasks(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasOpenTasks_EmptyOrNull_ReturnsFalse(string? json)
    {
        Assert.False(TaskBacklog.HasOpenTasks(json!));
    }

    [Fact]
    public void HasOpenTasks_InvalidJson_ReturnsFalse()
    {
        Assert.False(TaskBacklog.HasOpenTasks("{ not valid json }"));
    }

    [Fact]
    public void BuildBacklogJson_WithMalformedExistingBacklog_IgnoresAndCreatesNewBacklog()
    {
        var malformedBacklog = "{ this is not valid json }";
        var issuesJson = """
            [
              {
                "number": 1,
                "title": "Test",
                "body": "1. Task one",
                "state": "open"
              }
            ]
            """;

        var backlogJson = TaskBacklog.BuildBacklogJson(issuesJson, malformedBacklog);

        using var doc = JsonDocument.Parse(backlogJson);
        var tasks = doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();

        Assert.Single(tasks);
        Assert.Equal("open", tasks[0].GetProperty("status").GetString());
    }
}
