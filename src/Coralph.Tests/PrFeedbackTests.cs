using Coralph;

namespace Coralph.Tests;

public class PrFeedbackTests
{
    #region PrFeedbackComment Record Tests

    [Fact]
    public void PrFeedbackComment_CanBeCreated()
    {
        var comment = new PrFeedbackComment(
            Type: "mention",
            Author: "reviewer",
            Body: "@coralph please fix this",
            Path: "src/File.cs",
            Line: 10,
            IsResolved: false
        );

        Assert.Equal("mention", comment.Type);
        Assert.Equal("reviewer", comment.Author);
        Assert.Equal("@coralph please fix this", comment.Body);
        Assert.Equal("src/File.cs", comment.Path);
        Assert.Equal(10, comment.Line);
        Assert.False(comment.IsResolved);
    }

    [Fact]
    public void PrFeedbackComment_WithNullPath_AllowsNull()
    {
        var comment = new PrFeedbackComment(
            Type: "mention",
            Author: "reviewer",
            Body: "@coralph general comment",
            Path: null,
            Line: null,
            IsResolved: false
        );

        Assert.Null(comment.Path);
        Assert.Null(comment.Line);
    }

    [Fact]
    public void PrFeedbackComment_SupportsRecordEquality()
    {
        var comment1 = new PrFeedbackComment("mention", "user", "body", "file.cs", 10, false);
        var comment2 = new PrFeedbackComment("mention", "user", "body", "file.cs", 10, false);

        Assert.Equal(comment1, comment2);
    }

    [Fact]
    public void PrFeedbackComment_DifferentValues_NotEqual()
    {
        var comment1 = new PrFeedbackComment("mention", "user", "body1", "file.cs", 10, false);
        var comment2 = new PrFeedbackComment("mention", "user", "body2", "file.cs", 10, false);

        Assert.NotEqual(comment1, comment2);
    }

    #endregion

    #region PrFeedbackData Record Tests

    [Fact]
    public void PrFeedbackData_CanBeCreated()
    {
        var feedback = new PrFeedbackData(
            IssueNumber: 42,
            PrNumber: 57,
            PrBranch: "coralph/issue-42",
            Feedback: new List<PrFeedbackComment>
            {
                new PrFeedbackComment("mention", "reviewer", "@coralph fix", null, null, false)
            }
        );

        Assert.Equal(42, feedback.IssueNumber);
        Assert.Equal(57, feedback.PrNumber);
        Assert.Equal("coralph/issue-42", feedback.PrBranch);
        Assert.Single(feedback.Feedback);
    }

    [Fact]
    public void PrFeedbackData_WithMultipleComments_StoresAll()
    {
        var comments = new List<PrFeedbackComment>
        {
            new PrFeedbackComment("mention", "reviewer1", "@coralph fix A", "a.cs", 1, false),
            new PrFeedbackComment("unresolved_thread", "reviewer2", "Please change B", "b.cs", 2, false),
            new PrFeedbackComment("mention", "reviewer3", "@coralph fix C", "c.cs", 3, false)
        };

        var feedback = new PrFeedbackData(
            IssueNumber: 42,
            PrNumber: 57,
            PrBranch: "coralph/issue-42",
            Feedback: comments
        );

        Assert.Equal(3, feedback.Feedback.Count);
        Assert.Contains(feedback.Feedback, c => c.Body.Contains("fix A"));
        Assert.Contains(feedback.Feedback, c => c.Body.Contains("change B"));
        Assert.Contains(feedback.Feedback, c => c.Body.Contains("fix C"));
    }

    [Fact]
    public void PrFeedbackData_SupportsRecordEquality()
    {
        var comments = new List<PrFeedbackComment>
        {
            new PrFeedbackComment("mention", "user", "body", null, null, false)
        };

        var feedback1 = new PrFeedbackData(42, 57, "branch", comments);
        var feedback2 = new PrFeedbackData(42, 57, "branch", comments);

        Assert.Equal(feedback1, feedback2);
    }

    [Fact]
    public void PrFeedbackData_WithEmptyFeedbackList_IsValid()
    {
        var feedback = new PrFeedbackData(
            IssueNumber: 42,
            PrNumber: 57,
            PrBranch: "coralph/issue-42",
            Feedback: new List<PrFeedbackComment>()
        );

        Assert.Equal(42, feedback.IssueNumber);
        Assert.Empty(feedback.Feedback);
    }

    #endregion

    #region Feedback Filtering Scenarios

    [Fact]
    public void PrFeedbackComment_MentionType_HasCorrectType()
    {
        var comment = new PrFeedbackComment(
            Type: "mention",
            Author: "reviewer",
            Body: "@coralph please review",
            Path: null,
            Line: null,
            IsResolved: false
        );

        Assert.Equal("mention", comment.Type);
    }

    [Fact]
    public void PrFeedbackComment_UnresolvedThreadType_HasCorrectType()
    {
        var comment = new PrFeedbackComment(
            Type: "unresolved_thread",
            Author: "reviewer",
            Body: "This needs fixing",
            Path: "src/File.cs",
            Line: 42,
            IsResolved: false
        );

        Assert.Equal("unresolved_thread", comment.Type);
        Assert.False(comment.IsResolved);
    }

    [Fact]
    public void PrFeedbackComment_ResolvedComment_MarkedAsResolved()
    {
        var comment = new PrFeedbackComment(
            Type: "unresolved_thread",
            Author: "reviewer",
            Body: "This was fixed",
            Path: "src/File.cs",
            Line: 42,
            IsResolved: true
        );

        Assert.True(comment.IsResolved);
    }

    [Fact]
    public void PrFeedbackData_CanContainMixedCommentTypes()
    {
        var comments = new List<PrFeedbackComment>
        {
            new PrFeedbackComment("mention", "user1", "@coralph check", null, null, false),
            new PrFeedbackComment("unresolved_thread", "user2", "Fix this", "a.cs", 1, false),
            new PrFeedbackComment("unresolved_thread", "user3", "And this", "b.cs", 2, false)
        };

        var feedback = new PrFeedbackData(1, 2, "branch", comments);

        Assert.Equal(3, feedback.Feedback.Count);
        Assert.Single(feedback.Feedback.Where(c => c.Type == "mention"));
        Assert.Equal(2, feedback.Feedback.Count(c => c.Type == "unresolved_thread"));
    }

    #endregion
}
