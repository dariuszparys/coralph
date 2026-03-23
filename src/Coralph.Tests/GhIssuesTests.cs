using System.Diagnostics;
using Coralph;

namespace Coralph.Tests;

public sealed class GhIssuesTests
{
    [Fact]
    public void CreateFetchOpenIssuesProcessStartInfo_WithRepo_AddsSeparateArguments()
    {
        var psi = GhIssues.CreateFetchOpenIssuesProcessStartInfo("owner/repo with spaces");

        Assert.Equal("gh", psi.FileName);
        Assert.Equal(
            new[]
            {
                "issue",
                "list",
                "--state",
                "open",
                "--limit",
                "200",
                "--json",
                "number,title,body,url,labels,comments",
                "--repo",
                "owner/repo with spaces"
            },
            psi.ArgumentList.ToArray());
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.False(psi.UseShellExecute);
    }

    [Fact]
    public void CreateFetchOpenIssuesProcessStartInfo_WithNullRepo_OmitsOptionalArguments()
    {
        var psi = GhIssues.CreateFetchOpenIssuesProcessStartInfo(null);

        Assert.DoesNotContain("--repo", psi.ArgumentList);
        Assert.Equal(8, psi.ArgumentList.Count);
    }
}
