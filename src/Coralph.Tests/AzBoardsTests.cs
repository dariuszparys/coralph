using System.Diagnostics;
using Coralph;

namespace Coralph.Tests;

public sealed class AzBoardsTests
{
    [Fact]
    public void CreateFetchOpenWorkItemsProcessStartInfo_WithOrganizationAndProject_AddsSeparateArguments()
    {
        var psi = AzBoards.CreateFetchOpenWorkItemsProcessStartInfo(
            "https://dev.azure.com/org?query=1&view=all",
            "Project Alpha");

        Assert.Equal("az", psi.FileName);
        Assert.Equal(
            new[]
            {
                "boards",
                "query",
                "--wiql",
                "SELECT [System.Id], [System.Title], [System.Description], [System.State], [System.WorkItemType], [System.Tags] FROM workitems WHERE [System.State] <> 'Closed' AND [System.State] <> 'Removed' ORDER BY [System.ChangedDate] DESC",
                "--fields",
                "System.Id",
                "System.Title",
                "System.Description",
                "System.State",
                "System.WorkItemType",
                "System.Tags",
                "--output",
                "json",
                "--organization",
                "https://dev.azure.com/org?query=1&view=all",
                "--project",
                "Project Alpha"
            },
            psi.ArgumentList.ToArray());
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.False(psi.UseShellExecute);
    }

    [Fact]
    public void CreateFetchOpenWorkItemsProcessStartInfo_WithNullOptionalValues_OmitsOptionalArguments()
    {
        var psi = AzBoards.CreateFetchOpenWorkItemsProcessStartInfo(null, null);

        Assert.DoesNotContain("--organization", psi.ArgumentList);
        Assert.DoesNotContain("--project", psi.ArgumentList);
        Assert.Equal(13, psi.ArgumentList.Count);
    }
}
