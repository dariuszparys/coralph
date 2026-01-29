using Coralph;

namespace Coralph.Tests;

public class PromptHelpersPrModeTests
{
    #region BuildCombinedPrompt with PR Mode Tests

    [Fact]
    public void BuildCombinedPrompt_WithoutPrMode_DoesNotIncludePrModeHeader()
    {
        var template = "Do the work";
        var issues = "[]";
        var progress = "";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress, prMode: false);

        Assert.DoesNotContain("WORKFLOW MODE: PULL REQUEST", result);
        Assert.DoesNotContain("PR mode", result);
    }

    [Fact]
    public void BuildCombinedPrompt_WithPrMode_IncludesPrModeHeader()
    {
        var template = "Do the work";
        var issues = "[]";
        var progress = "";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress, prMode: true);

        Assert.Contains("WORKFLOW MODE: PULL REQUEST", result);
        Assert.Contains("You are operating in PR mode", result);
    }

    [Fact]
    public void BuildCombinedPrompt_WithPrModeTrue_WarnsAgainstDirectPush()
    {
        var template = "Do the work";
        var issues = "[]";
        var progress = "";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress, prMode: true);

        Assert.Contains("Do NOT push directly to main", result);
    }

    [Fact]
    public void BuildCombinedPrompt_WithoutPrFeedback_DoesNotIncludeFeedbackSection()
    {
        var template = "Do the work";
        var issues = "[]";
        var progress = "";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress);

        Assert.DoesNotContain("PR_FEEDBACK", result);
    }

    [Fact]
    public void BuildCombinedPrompt_WithEmptyPrFeedback_DoesNotIncludeFeedbackSection()
    {
        var template = "Do the work";
        var issues = "[]";
        var progress = "";
        var feedback = new Dictionary<int, PrFeedbackData>();

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress, prMode: true, feedback);

        Assert.DoesNotContain("PR_FEEDBACK", result);
    }

    [Fact]
    public void BuildCombinedPrompt_WithPrFeedback_IncludesFeedbackSection()
    {
        var template = "Do the work";
        var issues = "[{\"number\": 42}]";
        var progress = "";
        var feedback = new Dictionary<int, PrFeedbackData>
        {
            [42] = new PrFeedbackData(
                IssueNumber: 42,
                PrNumber: 57,
                PrBranch: "coralph/issue-42",
                Feedback: new List<PrFeedbackComment>
                {
                    new PrFeedbackComment(
                        Type: "mention",
                        Author: "reviewer",
                        Body: "@coralph please fix this",
                        Path: "src/File.cs",
                        Line: 10,
                        IsResolved: false
                    )
                }
            )
        };

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress, prMode: true, feedback);

        Assert.Contains("PR_FEEDBACK", result);
        Assert.Contains("@coralph please fix this", result);
        Assert.Contains("\"IssueNumber\": 42", result);
        Assert.Contains("\"PrNumber\": 57", result);
    }

    [Fact]
    public void BuildCombinedPrompt_WithMultipleFeedbackItems_IncludesAllInJson()
    {
        var template = "Do the work";
        var issues = "[{\"number\": 42}, {\"number\": 43}]";
        var progress = "";
        var feedback = new Dictionary<int, PrFeedbackData>
        {
            [42] = new PrFeedbackData(
                IssueNumber: 42,
                PrNumber: 57,
                PrBranch: "coralph/issue-42",
                Feedback: new List<PrFeedbackComment>
                {
                    new PrFeedbackComment("mention", "reviewer1", "@coralph fix A", null, null, false)
                }
            ),
            [43] = new PrFeedbackData(
                IssueNumber: 43,
                PrNumber: 58,
                PrBranch: "coralph/issue-43",
                Feedback: new List<PrFeedbackComment>
                {
                    new PrFeedbackComment("mention", "reviewer2", "@coralph fix B", null, null, false)
                }
            )
        };

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress, prMode: true, feedback);

        Assert.Contains("@coralph fix A", result);
        Assert.Contains("@coralph fix B", result);
        Assert.Contains("\"IssueNumber\": 42", result);
        Assert.Contains("\"IssueNumber\": 43", result);
    }

    [Fact]
    public void BuildCombinedPrompt_AlwaysIncludesIssuesJson()
    {
        var template = "Do the work";
        var issues = "[{\"number\": 1, \"title\": \"Test\"}]";
        var progress = "";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress);

        Assert.Contains("ISSUES_JSON", result);
        Assert.Contains("\"number\": 1", result);
        Assert.Contains("\"title\": \"Test\"", result);
    }

    [Fact]
    public void BuildCombinedPrompt_AlwaysIncludesProgressSection()
    {
        var template = "Do the work";
        var issues = "[]";
        var progress = "## 2026-01-29 - Issue #1\n- Did something";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress);

        Assert.Contains("PROGRESS_SO_FAR", result);
        Assert.Contains("Did something", result);
    }

    [Fact]
    public void BuildCombinedPrompt_WithEmptyProgress_ShowsEmptyPlaceholder()
    {
        var template = "Do the work";
        var issues = "[]";
        var progress = "";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress);

        Assert.Contains("PROGRESS_SO_FAR", result);
        Assert.Contains("(empty)", result);
    }

    [Fact]
    public void BuildCombinedPrompt_AlwaysIncludesInstructions()
    {
        var template = "Do the work carefully";
        var issues = "[]";
        var progress = "";

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress);

        Assert.Contains("INSTRUCTIONS", result);
        Assert.Contains("Do the work carefully", result);
    }

    [Fact]
    public void BuildCombinedPrompt_OrdersContentCorrectly()
    {
        var template = "Do the work";
        var issues = "[{\"number\": 1}]";
        var progress = "Some progress was made";
        var feedback = new Dictionary<int, PrFeedbackData>
        {
            [1] = new PrFeedbackData(1, 10, "branch", new List<PrFeedbackComment>
            {
                new PrFeedbackComment("mention", "user", "@coralph test", null, null, false)
            })
        };

        var result = PromptHelpers.BuildCombinedPrompt(template, issues, progress, prMode: true, feedback);

        var issuesIndex = result.IndexOf("# ISSUES_JSON");
        var feedbackIndex = result.IndexOf("# PR_FEEDBACK");
        var progressIndex = result.IndexOf("# PROGRESS_SO_FAR");
        var instructionsIndex = result.IndexOf("# INSTRUCTIONS");

        // Verify the order (all should be found)
        Assert.True(issuesIndex > 0, "ISSUES_JSON not found");
        Assert.True(feedbackIndex > 0, "PR_FEEDBACK not found");
        Assert.True(progressIndex > 0, "PROGRESS_SO_FAR not found");
        Assert.True(instructionsIndex > 0, "INSTRUCTIONS not found");
        
        // Verify correct ordering
        Assert.True(issuesIndex < feedbackIndex, $"Issues ({issuesIndex}) should come before Feedback ({feedbackIndex})");
        Assert.True(feedbackIndex < progressIndex, $"Feedback ({feedbackIndex}) should come before Progress ({progressIndex})");
        Assert.True(progressIndex < instructionsIndex, $"Progress ({progressIndex}) should come before Instructions ({instructionsIndex})");
    }

    #endregion
}
