using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Infrastructure.Git;

/// <summary>
/// 提供基础仓库初始化、提交、差异比较与历史读取能力。
/// Provides repository initialization, commits, diffs, and history access.
/// </summary>
public class GitService
{
    private readonly ILogger<GitService> _logger;

    /// <summary>
    /// 初始化 Git 服务实例。
    /// Initializes the Git service instance.
    /// </summary>
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化 Git 仓库。
    /// Initializes a Git repository.
    /// </summary>
    /// <param name="path">仓库工作目录。/ The repository working directory.</param>
    /// <returns>可用于后续操作的仓库路径。/ The repository path for later operations.</returns>
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
    /// 暂存并提交仓库中的全部变更。
    /// Stages and commits all changes in the repository.
    /// </summary>
    /// <param name="repoPath">仓库路径。/ The repository path.</param>
    /// <param name="message">提交消息。/ The commit message.</param>
    /// <param name="authorName">提交作者名称。/ The commit author name.</param>
    /// <param name="authorEmail">提交作者邮箱。/ The commit author email.</param>
    /// <returns>新提交的 SHA；如果没有变更则返回 <see langword="null"/>。/ The new commit SHA, or <see langword="null"/> when nothing changed.</returns>
    public string? CommitAll(string repoPath, string message, string authorName = "OpenStaff", string authorEmail = "openstaff@local")
    {
        using var repo = new Repository(repoPath);

        // zh-CN: 先统一暂存全部工作树变更，再根据最终状态判断是否真的需要创建提交。
        // en: Stage the full working tree first, then inspect the resulting status to decide whether a real commit is necessary.
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
    /// 获取两个提交之间的文本差异。
    /// Gets the textual diff between two commits.
    /// </summary>
    /// <param name="repoPath">仓库路径。/ The repository path.</param>
    /// <param name="fromSha">起始提交；为空时表示从空树开始比较。/ The starting commit; <see langword="null"/> means compare from an empty tree.</param>
    /// <param name="toSha">目标提交；为空时默认使用当前 HEAD。/ The target commit; <see langword="null"/> defaults to the current HEAD.</param>
    /// <returns>统一 diff 文本。/ The unified diff text.</returns>
    public string GetDiff(string repoPath, string? fromSha, string? toSha)
    {
        using var repo = new Repository(repoPath);

        // zh-CN: 允许调用方省略任一端的提交号，便于查看初始化快照或当前 HEAD 的完整变化。
        // en: Callers may omit either commit so they can inspect an initial snapshot or the complete delta to the current HEAD.
        var fromCommit = fromSha != null ? repo.Lookup<Commit>(fromSha) : null;
        var toCommit = toSha != null ? repo.Lookup<Commit>(toSha) : repo.Head.Tip;

        var fromTree = fromCommit?.Tree;
        var toTree = toCommit?.Tree;

        var diff = repo.Diff.Compare<Patch>(fromTree, toTree);
        return diff.Content;
    }

    /// <summary>
    /// 获取最近的提交历史摘要。
    /// Gets a summary of recent commit history.
    /// </summary>
    /// <param name="repoPath">仓库路径。/ The repository path.</param>
    /// <param name="maxCount">返回的最大提交数量。/ The maximum number of commits to return.</param>
    /// <returns>最近提交的摘要集合。/ A collection of recent commit summaries.</returns>
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

/// <summary>
/// 提交元数据快照。
/// Snapshot of commit metadata.
/// </summary>
public class CommitInfo
{
    /// <summary>
    /// 完整提交 SHA。
    /// Full commit SHA.
    /// </summary>
    public string Sha { get; set; } = string.Empty;

    /// <summary>
    /// 用于界面展示的短 SHA。
    /// Short SHA for UI display.
    /// </summary>
    public string ShortSha { get; set; } = string.Empty;

    /// <summary>
    /// 提交标题。
    /// Commit subject line.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 提交作者显示名。
    /// Display name of the commit author.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 提交时间（UTC）。
    /// Commit timestamp in UTC.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
