using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Infrastructure.Git;

/// <summary>
/// Git 操作服务 / Git operations service
/// </summary>
public class GitService
{
    private readonly ILogger<GitService> _logger;

    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化 Git 仓库 / Initialize a Git repository
    /// </summary>
    public string InitRepository(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        if (!Repository.IsValid(path))
        {
            Repository.Init(path);
            _logger.LogInformation("Git 仓库已初始化: {Path}", path);
        }

        return path;
    }

    /// <summary>
    /// 提交所有变更 / Commit all changes
    /// </summary>
    public string? CommitAll(string repoPath, string message, string authorName = "OpenStaff", string authorEmail = "openstaff@local")
    {
        using var repo = new Repository(repoPath);

        Commands.Stage(repo, "*");

        var status = repo.RetrieveStatus();
        if (!status.IsDirty)
        {
            _logger.LogInformation("无变更需要提交 / No changes to commit");
            return null;
        }

        var author = new Signature(authorName, authorEmail, DateTimeOffset.Now);
        var commit = repo.Commit(message, author, author);

        _logger.LogInformation("已提交: {Sha} - {Message}", commit.Sha[..8], message);
        return commit.Sha;
    }

    /// <summary>
    /// 获取两个 commit 之间的 diff / Get diff between two commits
    /// </summary>
    public string GetDiff(string repoPath, string? fromSha, string? toSha)
    {
        using var repo = new Repository(repoPath);

        var fromCommit = fromSha != null ? repo.Lookup<Commit>(fromSha) : null;
        var toCommit = toSha != null ? repo.Lookup<Commit>(toSha) : repo.Head.Tip;

        var fromTree = fromCommit?.Tree;
        var toTree = toCommit?.Tree;

        var diff = repo.Diff.Compare<Patch>(fromTree, toTree);
        return diff.Content;
    }

    /// <summary>
    /// 获取提交历史 / Get commit history
    /// </summary>
    public IEnumerable<CommitInfo> GetHistory(string repoPath, int maxCount = 50)
    {
        using var repo = new Repository(repoPath);

        return repo.Commits
            .Take(maxCount)
            .Select(c => new CommitInfo
            {
                Sha = c.Sha,
                ShortSha = c.Sha[..8],
                Message = c.MessageShort,
                Author = c.Author.Name,
                Timestamp = c.Author.When.UtcDateTime
            })
            .ToList();
    }
}

public class CommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
