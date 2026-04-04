using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Agents.Producer;

/// <summary>
/// Git 操作管理器，封装 Producer 所需的 Git 功能 / Git manager wrapping Git operations for the Producer agent
/// </summary>
public class GitManager
{
    private readonly ILogger _logger;

    public GitManager(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 提交所有变更 / Commit all changes
    /// </summary>
    /// <param name="workspacePath">工作空间路径 / Workspace path</param>
    /// <param name="message">提交消息 / Commit message</param>
    /// <param name="authorName">作者名称 / Author name</param>
    /// <returns>提交 SHA，失败返回 null / Commit SHA, or null on failure</returns>
    public string? CommitChanges(string workspacePath, string message, string authorName = "OpenStaff-Producer")
    {
        try
        {
            // 确保 Git 仓库已初始化 / Ensure Git repository is initialized
            EnsureGitRepository(workspacePath);

            // 暂存所有变更 / Stage all changes
            var stageResult = RunGitCommand(workspacePath, "add -A");
            if (!stageResult.Success)
            {
                _logger.LogWarning("Git add 失败 / Git add failed: {Error}", stageResult.Error);
                return null;
            }

            // 检查是否有变更需要提交 / Check if there are changes to commit
            var statusResult = RunGitCommand(workspacePath, "status --porcelain");
            if (!statusResult.Success || string.IsNullOrWhiteSpace(statusResult.Output))
            {
                _logger.LogInformation("无变更需要提交 / No changes to commit");
                return null;
            }

            // 提交变更 / Commit changes
            var escapedMessage = message.Replace("\"", "\\\"");
            var commitResult = RunGitCommand(workspacePath,
                $"commit -m \"{escapedMessage}\" --author=\"{authorName} <{authorName.ToLowerInvariant().Replace(' ', '-')}@openstaff.local>\"");

            if (!commitResult.Success)
            {
                _logger.LogWarning("Git commit 失败 / Git commit failed: {Error}", commitResult.Error);
                return null;
            }

            // 获取最新提交 SHA / Get latest commit SHA
            var shaResult = RunGitCommand(workspacePath, "rev-parse HEAD");
            if (shaResult.Success && !string.IsNullOrWhiteSpace(shaResult.Output))
            {
                var sha = shaResult.Output.Trim();
                _logger.LogInformation("已提交: {Sha} - {Message} / Committed: {Sha} - {Message}",
                    sha[..Math.Min(8, sha.Length)], message, sha[..Math.Min(8, sha.Length)], message);
                return sha;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git 提交失败 / Git commit failed");
            return null;
        }
    }

    /// <summary>
    /// 获取当前变更摘要 / Get a diff summary of current changes
    /// </summary>
    /// <param name="workspacePath">工作空间路径 / Workspace path</param>
    /// <returns>变更摘要 / Diff summary</returns>
    public string GetDiffSummary(string workspacePath)
    {
        try
        {
            // 获取已暂存和未暂存的变更统计 / Get staged and unstaged change stats
            var diffResult = RunGitCommand(workspacePath, "diff --stat");
            var stagedResult = RunGitCommand(workspacePath, "diff --cached --stat");

            var summary = "";
            if (stagedResult.Success && !string.IsNullOrWhiteSpace(stagedResult.Output))
                summary += $"已暂存变更 / Staged changes:\n{stagedResult.Output}\n";
            if (diffResult.Success && !string.IsNullOrWhiteSpace(diffResult.Output))
                summary += $"未暂存变更 / Unstaged changes:\n{diffResult.Output}\n";

            if (string.IsNullOrWhiteSpace(summary))
                summary = "无变更 / No changes";

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 diff 摘要失败 / Failed to get diff summary");
            return "获取变更摘要失败 / Failed to get diff summary";
        }
    }

    /// <summary>
    /// 确保工作空间是 Git 仓库 / Ensure the workspace is a Git repository
    /// </summary>
    private void EnsureGitRepository(string workspacePath)
    {
        var gitDir = Path.Combine(workspacePath, ".git");
        if (!Directory.Exists(gitDir))
        {
            _logger.LogInformation("初始化 Git 仓库: {Path} / Initializing Git repository: {Path}",
                workspacePath, workspacePath);
            RunGitCommand(workspacePath, "init");
            // 设置默认分支名 / Set default branch name
            RunGitCommand(workspacePath, "branch -M main");
        }
    }

    /// <summary>
    /// 执行 Git 命令 / Run a Git command
    /// </summary>
    private static GitCommandResult RunGitCommand(string workingDirectory, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return new GitCommandResult { Success = false, Error = "无法启动 Git 进程 / Failed to start Git process" };

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000); // 30秒超时 / 30-second timeout

            return new GitCommandResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            return new GitCommandResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Git 命令执行结果 / Git command result
    /// </summary>
    private class GitCommandResult
    {
        public bool Success { get; init; }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
    }
}
