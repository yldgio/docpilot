using Octokit;

namespace DocPilot.GitHub;

public sealed class GitHubService
{
    private readonly GitHubClient _client;
    private readonly string _owner;
    private readonly string _repo;

    public GitHubService(string owner, string repo, string? token = null)
    {
        _owner = owner;
        _repo = repo;

        _client = new GitHubClient(new ProductHeaderValue("DocPilot"));

        var effectiveToken = token ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(effectiveToken))
        {
            _client.Credentials = new Credentials(effectiveToken);
        }
    }

    public async Task<BranchReference> CreateBranchAsync(
        string branchName,
        string baseBranch = "main",
        CancellationToken cancellationToken = default)
    {
        // Get the SHA of the base branch
        var baseBranchRef = await _client.Git.Reference.Get(_owner, _repo, $"refs/heads/{baseBranch}");
        var baseSha = baseBranchRef.Object.Sha;

        // Create new branch
        var newBranch = await _client.Git.Reference.Create(_owner, _repo, new NewReference(
            $"refs/heads/{branchName}",
            baseSha
        ));

        return new BranchReference
        {
            Name = branchName,
            Sha = newBranch.Object.Sha,
            Url = newBranch.Url
        };
    }

    public async Task<CommitReference> CommitFilesAsync(
        string branchName,
        IReadOnlyList<FileChange> files,
        string message,
        CancellationToken cancellationToken = default)
    {
        // Get the current commit SHA
        var branchRef = await _client.Git.Reference.Get(_owner, _repo, $"refs/heads/{branchName}");
        var parentSha = branchRef.Object.Sha;

        // Get the tree SHA
        var parentCommit = await _client.Git.Commit.Get(_owner, _repo, parentSha);
        var baseTreeSha = parentCommit.Tree.Sha;

        // Create tree items for each file
        var treeItems = files.Select(f => new NewTreeItem
        {
            Path = f.Path,
            Mode = "100644",
            Type = TreeType.Blob,
            Content = f.Content
        }).ToList();

        // Create new tree
        var newTree = await _client.Git.Tree.Create(_owner, _repo, new NewTree
        {
            BaseTree = baseTreeSha,
            Tree = { }
        });

        // Add items to tree
        var createTree = new NewTree { BaseTree = baseTreeSha };
        foreach (var item in treeItems)
        {
            createTree.Tree.Add(item);
        }
        var tree = await _client.Git.Tree.Create(_owner, _repo, createTree);

        // Create commit
        var newCommit = new NewCommit(message, tree.Sha, parentSha);
        var commit = await _client.Git.Commit.Create(_owner, _repo, newCommit);

        // Update branch reference
        await _client.Git.Reference.Update(_owner, _repo, $"refs/heads/{branchName}", new ReferenceUpdate(commit.Sha));

        return new CommitReference
        {
            Sha = commit.Sha,
            Message = message,
            Url = $"https://github.com/{_owner}/{_repo}/commit/{commit.Sha}"
        };
    }

    public async Task<PullRequestReference> CreatePullRequestAsync(
        string title,
        string body,
        string headBranch,
        string baseBranch = "main",
        bool draft = false,
        IReadOnlyList<string>? labels = null,
        CancellationToken cancellationToken = default)
    {
        var newPr = new NewPullRequest(title, headBranch, baseBranch)
        {
            Body = body,
            Draft = draft
        };

        var pr = await _client.PullRequest.Create(_owner, _repo, newPr);

        // Add labels if specified
        if (labels is { Count: > 0 })
        {
            await _client.Issue.Labels.AddToIssue(_owner, _repo, pr.Number, labels.ToArray());
        }

        return new PullRequestReference
        {
            Number = pr.Number,
            Title = pr.Title,
            Url = pr.HtmlUrl,
            State = pr.State.StringValue
        };
    }

    public async Task<Repository> GetRepositoryAsync(CancellationToken cancellationToken = default)
    {
        return await _client.Repository.Get(_owner, _repo);
    }

    public static (string Owner, string Repo) ParseRemoteUrl(string remoteUrl)
    {
        // Handle SSH URLs: git@github.com:owner/repo.git
        if (remoteUrl.StartsWith("git@"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                remoteUrl, @"git@github\.com[:/](?<owner>[^/]+)/(?<repo>[^.]+)(?:\.git)?$");
            if (match.Success)
            {
                return (match.Groups["owner"].Value, match.Groups["repo"].Value);
            }
        }

        // Handle HTTPS URLs: https://github.com/owner/repo.git
        if (remoteUrl.StartsWith("https://"))
        {
            var uri = new Uri(remoteUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                var repo = segments[1].Replace(".git", "");
                return (segments[0], repo);
            }
        }

        throw new ArgumentException($"Could not parse remote URL: {remoteUrl}");
    }
}

public sealed record BranchReference
{
    public required string Name { get; init; }
    public required string Sha { get; init; }
    public required string Url { get; init; }
}

public sealed record CommitReference
{
    public required string Sha { get; init; }
    public required string Message { get; init; }
    public required string Url { get; init; }
}

public sealed record PullRequestReference
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string State { get; init; }
}

public sealed record FileChange
{
    public required string Path { get; init; }
    public required string Content { get; init; }
}
