# DocPilot MVP - Implementation Guide (Part 8)

## Step 8: GitHub PR Integration

### Step-by-Step Instructions

#### 8.1 Creare directory GitHub
- [x] Esegui:

```powershell
New-Item -ItemType Directory -Path "src/DocPilot/GitHub" -Force
```

#### 8.2 Creare GitHubService.cs
- [x] Crea il file `src/DocPilot/GitHub/GitHubService.cs`:

```csharp
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
            Url = commit.HtmlUrl
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
```

#### 8.3 Creare PullRequestCreator.cs
- [x] Crea il file `src/DocPilot/GitHub/PullRequestCreator.cs`:

```csharp
using DocPilot.Configuration;
using DocPilot.Heuristics;
using LibGit2Sharp;

namespace DocPilot.GitHub;

public sealed class PullRequestCreator
{
    private readonly GitHubService _github;
    private readonly string _repositoryPath;
    private readonly DocPilotConfig _config;

    public PullRequestCreator(string repositoryPath, DocPilotConfig config)
    {
        _repositoryPath = repositoryPath;
        _config = config;

        using var repo = new Repository(repositoryPath);
        var remote = repo.Network.Remotes["origin"];
        if (remote is null)
        {
            throw new InvalidOperationException("No 'origin' remote found in repository");
        }

        var (owner, repoName) = GitHubService.ParseRemoteUrl(remote.Url);
        _github = new GitHubService(owner, repoName);
    }

    public async Task<PrCreationResult> CreateDocumentationPrAsync(
        MappingResult mapping,
        IReadOnlyList<string> changedFiles,
        PrOptions options,
        CancellationToken cancellationToken = default)
    {
        var branchName = GenerateBranchName(mapping);
        var prTitle = options.Title ?? GeneratePrTitle(mapping);
        var prBody = GeneratePrBody(mapping, changedFiles);
        var labels = DetermineLabels(mapping);
        var isDraft = options.Draft || mapping.OverallConfidence == ConfidenceLevel.Low;

        try
        {
            // Create branch
            var baseBranch = options.TargetBranch ?? GetDefaultBranch();
            var branch = await _github.CreateBranchAsync(branchName, baseBranch, cancellationToken);

            // Read and commit changed files
            var fileChanges = changedFiles.Select(path =>
            {
                var fullPath = Path.Combine(_repositoryPath, path);
                var content = File.ReadAllText(fullPath);
                return new FileChange
                {
                    Path = path,
                    Content = content
                };
            }).ToList();

            var commitMessage = GenerateCommitMessage(mapping);
            var commit = await _github.CommitFilesAsync(branchName, fileChanges, commitMessage, cancellationToken);

            // Create PR
            var pr = await _github.CreatePullRequestAsync(
                prTitle,
                prBody,
                branchName,
                baseBranch,
                isDraft,
                labels,
                cancellationToken);

            return new PrCreationResult
            {
                Success = true,
                PullRequest = pr,
                Branch = branch,
                Commit = commit
            };
        }
        catch (Exception ex)
        {
            return new PrCreationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private string GenerateBranchName(MappingResult mapping)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var context = mapping.OverallChangeType.ToString().ToLowerInvariant();
        return $"docpilot/{context}-{timestamp}";
    }

    private string GeneratePrTitle(MappingResult mapping)
    {
        var emoji = mapping.OverallChangeType switch
        {
            ChangeType.NewFeature => "✨",
            ChangeType.BugFix => "🐛",
            ChangeType.Refactoring => "♻️",
            ChangeType.ApiChange => "📚",
            _ => "📝"
        };

        var action = mapping.Targets.Count switch
        {
            1 => $"Update {Path.GetFileName(mapping.Targets[0].FilePath)}",
            _ => $"Update {mapping.Targets.Count} documentation files"
        };

        return $"{emoji} docs: {action}";
    }

    private string GeneratePrBody(MappingResult mapping, IReadOnlyList<string> changedFiles)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## 📝 Documentation Update");
        sb.AppendLine();
        sb.AppendLine("This PR was automatically generated by **DocPilot** to keep documentation in sync with code changes.");
        sb.AppendLine();
        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Change Type:** {mapping.OverallChangeType}");
        sb.AppendLine($"- **Confidence:** {mapping.OverallConfidence} ({mapping.AverageConfidence:P0})");
        sb.AppendLine($"- **Files Updated:** {changedFiles.Count}");
        sb.AppendLine();
        sb.AppendLine("### Changes");
        sb.AppendLine();

        foreach (var file in changedFiles)
        {
            sb.AppendLine($"- `{file}`");
        }

        sb.AppendLine();
        sb.AppendLine("### Affected Documentation Targets");
        sb.AppendLine();

        foreach (var target in mapping.Targets)
        {
            sb.AppendLine($"#### {target.FilePath}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(target.Section))
            {
                sb.AppendLine($"- **Section:** {target.Section}");
            }
            sb.AppendLine($"- **Confidence:** {target.Confidence} ({target.ConfidenceScore:P0})");
            sb.AppendLine($"- **Source Files:** {string.Join(", ", target.SourceFiles.Select(f => $"`{f}`"))}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated with ❤️ by [DocPilot](https://github.com/your-org/docpilot)*");

        return sb.ToString();
    }

    private string GenerateCommitMessage(MappingResult mapping)
    {
        var scope = mapping.Targets.Count == 1
            ? Path.GetFileNameWithoutExtension(mapping.Targets[0].FilePath)
            : "multiple";

        return $"docs({scope}): update documentation for {mapping.OverallChangeType.ToString().ToLowerInvariant()} changes";
    }

    private List<string> DetermineLabels(MappingResult mapping)
    {
        var labels = new List<string> { "documentation", "docpilot" };

        labels.Add(mapping.OverallConfidence switch
        {
            ConfidenceLevel.High => "ready-for-review",
            ConfidenceLevel.Medium => "needs-review",
            ConfidenceLevel.Low => "draft",
            _ => "needs-review"
        });

        return labels;
    }

    private string GetDefaultBranch()
    {
        using var repo = new Repository(_repositoryPath);
        return repo.Head.FriendlyName.Contains("main") ? "main" : "master";
    }
}

public sealed record PrOptions
{
    public string? Title { get; init; }
    public string? TargetBranch { get; init; }
    public bool Draft { get; init; }
}

public sealed record PrCreationResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public PullRequestReference? PullRequest { get; init; }
    public BranchReference? Branch { get; init; }
    public CommitReference? Commit { get; init; }
}
```

#### 8.4 Aggiornare PrCommand.cs
- [x] Sostituisci il contenuto di `src/DocPilot/Commands/PrCommand.cs`:

```csharp
using System.CommandLine;
using DocPilot.Configuration;
using DocPilot.GitHub;
using DocPilot.Pipeline;

namespace DocPilot.Commands;

public static class PrCommand
{
    public static Command Create()
    {
        var targetBranchOption = new Option<string?>(
            aliases: ["--target-branch", "-t"],
            description: "Target branch for the PR (default: main)");

        var draftOption = new Option<bool>(
            aliases: ["--draft", "-d"],
            description: "Create PR as draft");

        var titleOption = new Option<string?>(
            aliases: ["--title"],
            description: "PR title (auto-generated if not provided)");

        var baseOption = new Option<string?>(
            aliases: ["--base", "-b"],
            description: "Base commit/branch for diff comparison");

        var headOption = new Option<string?>(
            aliases: ["--head", "-h"],
            description: "Head commit/branch for diff comparison");

        var stagedOption = new Option<bool>(
            aliases: ["--staged", "-s"],
            description: "Analyze staged changes only");

        var configOption = new Option<string?>(
            aliases: ["--config", "-c"],
            description: "Path to docpilot.yml configuration file");

        var command = new Command("pr", "Create a documentation pull request")
        {
            targetBranchOption,
            draftOption,
            titleOption,
            baseOption,
            headOption,
            stagedOption,
            configOption
        };

        command.SetHandler(async (context) =>
        {
            var targetBranch = context.ParseResult.GetValueForOption(targetBranchOption);
            var draft = context.ParseResult.GetValueForOption(draftOption);
            var title = context.ParseResult.GetValueForOption(titleOption);
            var baseRef = context.ParseResult.GetValueForOption(baseOption);
            var headRef = context.ParseResult.GetValueForOption(headOption);
            var staged = context.ParseResult.GetValueForOption(stagedOption);
            var configPath = context.ParseResult.GetValueForOption(configOption);
            var cancellationToken = context.GetCancellationToken();

            var loader = new ConfigurationLoader();
            var config = await loader.LoadAsync(configPath, cancellationToken);
            var repoPath = Directory.GetCurrentDirectory();

            // Run full pipeline
            await using var pipeline = new DocumentationPipeline(repoPath, config);
            await pipeline.InitializeAsync(cancellationToken);

            // 1. Analyze
            Console.WriteLine("📊 Analyzing changes...");
            var analysisResult = await pipeline.RunAnalyzeAsync(baseRef, headRef, staged, cancellationToken);

            if (!analysisResult.Success || analysisResult.Mapping is null)
            {
                Console.Error.WriteLine($"❌ Analysis failed: {analysisResult.Error}");
                context.ExitCode = 1;
                return;
            }

            // 2. Generate
            Console.WriteLine("✍️  Generating documentation...");
            var generateResult = await pipeline.RunGenerateAsync(analysisResult.Mapping, dryRun: false, cancellationToken);

            if (!generateResult.Success || generateResult.GeneratedFiles is null)
            {
                Console.Error.WriteLine($"❌ Generation failed: {generateResult.Error}");
                context.ExitCode = 1;
                return;
            }

            // 3. Create PR
            Console.WriteLine("🚀 Creating pull request...");
            var prCreator = new PullRequestCreator(repoPath, config);
            var prResult = await prCreator.CreateDocumentationPrAsync(
                analysisResult.Mapping,
                generateResult.GeneratedFiles,
                new PrOptions
                {
                    Title = title,
                    TargetBranch = targetBranch,
                    Draft = draft
                },
                cancellationToken);

            if (!prResult.Success)
            {
                Console.Error.WriteLine($"❌ PR creation failed: {prResult.Error}");
                context.ExitCode = 1;
                return;
            }

            Console.WriteLine($"\n✅ Documentation PR created successfully!");
            Console.WriteLine($"   Branch: {prResult.Branch?.Name}");
            Console.WriteLine($"   PR: #{prResult.PullRequest?.Number}");
            Console.WriteLine($"   URL: {prResult.PullRequest?.Url}");

            context.ExitCode = 0;
        });

        return command;
    }
}
```

### Step 8 Tests

#### 8.5 Creare test per GitHubService
- [x] Crea il file `tests/DocPilot.Tests/GitHub/GitHubServiceTests.cs`:

```csharp
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
```

### Step 8 Verification Checklist
- [x] `dotnet build` compila senza errori
- [x] `dotnet test` passa tutti i test
- [x] `dotnet run --project src/DocPilot -- pr --help` mostra tutte le opzioni

### Step 8 STOP & COMMIT
**STOP & COMMIT:** Fermarsi qui e attendere che l'utente testi, faccia stage e commit.

Messaggio commit suggerito:
```
feat(github): implement PR creation with Octokit

- Add GitHubService for branch, commit, and PR operations
- Add PullRequestCreator for orchestrating doc PR workflow
- Update PrCommand to run full analyze -> generate -> PR pipeline
- Add tests for URL parsing
- Support draft PRs and confidence-based labels
```
