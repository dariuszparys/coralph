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
    public void ParseGitHubUrl_HttpsUrlWithDots_ReturnsOwnerAndRepo()
    {
        var url = "https://github.com/my-org/my.repo.git";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("my-org", owner);
        Assert.Equal("my.repo", repo);
    }

    [Fact]
    public void ParseGitHubUrl_SshUrlWithDots_ReturnsOwnerAndRepo()
    {
        var url = "git@github.com:my-org/my.repo.git";

        var (owner, repo) = GitPermissions.ParseGitHubUrl(url);

        Assert.Equal("my-org", owner);
        Assert.Equal("my.repo", repo);
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

    #region IsUserInBypassList Tests

    [Fact]
    public void IsUserInBypassList_WithMatchingLogin_ReturnsTrue()
    {
        var result = GitPermissions.IsUserInBypassList("dariuszparys", ["dariuszparys"]);

        Assert.True(result);
    }

    [Fact]
    public void IsUserInBypassList_WithDifferentLogin_ReturnsFalse()
    {
        var result = GitPermissions.IsUserInBypassList("dariuszparys", ["someoneelse"]);

        Assert.False(result);
    }

    [Fact]
    public void IsUserInBypassList_WithNullLogin_ReturnsFalse()
    {
        var result = GitPermissions.IsUserInBypassList(null, ["dariuszparys"]);

        Assert.False(result);
    }

    [Fact]
    public void IsUserInBypassList_WithCaseInsensitiveMatch_ReturnsTrue()
    {
        var result = GitPermissions.IsUserInBypassList("DARIUSZPARYS", ["dariuszparys"]);

        Assert.True(result);
    }

    #endregion
}
