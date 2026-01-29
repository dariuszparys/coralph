using Coralph;

namespace Coralph.Tests;

public class GitPermissionsTests
{
    #region ParseGitHubUrl Tests

    [Fact]
    public void ParseGitHubUrl_HttpsUrl_ReturnsOwnerAndRepo()
    {
        var url = "https://github.com/owner/repo.git";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
    }

    [Fact]
    public void ParseGitHubUrl_HttpsUrlWithoutGitExtension_ReturnsOwnerAndRepo()
    {
        var url = "https://github.com/owner/repo";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
    }

    [Fact]
    public void ParseGitHubUrl_SshUrl_ReturnsOwnerAndRepo()
    {
        var url = "git@github.com:owner/repo.git";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
    }

    [Fact]
    public void ParseGitHubUrl_SshUrlWithoutGitExtension_ReturnsOwnerAndRepo()
    {
        var url = "git@github.com:owner/repo";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
    }

    [Fact]
    public void ParseGitHubUrl_HttpsUrlWithDashes_ReturnsOwnerAndRepo()
    {
        var url = "https://github.com/my-org/my-repo.git";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("my-org", owner);
        Assert.Equal("my-repo", repo);
    }

    [Fact]
    public void ParseGitHubUrl_SshUrlWithDashes_ReturnsOwnerAndRepo()
    {
        var url = "git@github.com:my-org/my-repo.git";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("my-org", owner);
        Assert.Equal("my-repo", repo);
    }

    [Fact]
    public void ParseGitHubUrl_InvalidUrl_ReturnsNulls()
    {
        var url = "https://gitlab.com/owner/repo.git";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Null(owner);
        Assert.Null(repo);
    }

    [Fact]
    public void ParseGitHubUrl_EmptyString_ReturnsNulls()
    {
        var url = "";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Null(owner);
        Assert.Null(repo);
    }

    [Fact]
    public void ParseGitHubUrl_MalformedUrl_ReturnsNulls()
    {
        var url = "not-a-url";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Null(owner);
        Assert.Null(repo);
    }

    #endregion
}
