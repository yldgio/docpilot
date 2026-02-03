using DocPilot.GitHub;
using Xunit;

namespace DocPilot.Tests.GitHub;

public class GitHubServiceTests
{
    [Theory]
    [InlineData("git@github.com:owner/repo.git", "owner", "repo")]
    [InlineData("git@github.com:owner/repo", "owner", "repo")]
    [InlineData("https://github.com/owner/repo.git", "owner", "repo")]
    [InlineData("https://github.com/owner/repo", "owner", "repo")]
    public void ParseRemoteUrl_ValidUrls_ReturnsOwnerAndRepo(string url, string expectedOwner, string expectedRepo)
    {
        var (owner, repo) = GitHubService.ParseRemoteUrl(url);

        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedRepo, repo);
    }

    [Theory]
    [InlineData("invalid-url")]
    [InlineData("ftp://github.com/owner/repo")]
    public void ParseRemoteUrl_InvalidUrl_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() => GitHubService.ParseRemoteUrl(url));
    }
}
