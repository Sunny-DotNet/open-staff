using Microsoft.Extensions.Logging;

namespace OpenStaff.Agents.Producer;

/// <summary>
/// 代码生成辅助类，封装文件操作 / Code generation helper for file operations
/// </summary>
public class CodeGenerator
{
    private readonly ILogger _logger;

    public CodeGenerator(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 创建文件 / Create a new file
    /// </summary>
    /// <param name="workspacePath">工作空间根路径 / Workspace root path</param>
    /// <param name="relativePath">相对路径 / Relative file path</param>
    /// <param name="content">文件内容 / File content</param>
    /// <returns>是否成功 / Whether the operation succeeded</returns>
    public bool CreateFile(string workspacePath, string relativePath, string content)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));

            // 安全检查：确保目标路径在工作空间内 / Safety check: ensure path is within workspace
            if (!fullPath.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("路径越界: {Path} / Path escape detected: {Path}", relativePath, relativePath);
                return false;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content);
            _logger.LogInformation("文件已创建: {Path} / File created: {Path}", relativePath, relativePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建文件失败: {Path} / Failed to create file: {Path}", relativePath, relativePath);
            return false;
        }
    }

    /// <summary>
    /// 编辑文件（替换内容片段） / Edit a file by replacing a content segment
    /// </summary>
    /// <param name="workspacePath">工作空间根路径 / Workspace root path</param>
    /// <param name="relativePath">相对路径 / Relative file path</param>
    /// <param name="oldContent">要替换的旧内容 / Old content to replace</param>
    /// <param name="newContent">替换后的新内容 / New content to insert</param>
    /// <returns>是否成功 / Whether the operation succeeded</returns>
    public bool EditFile(string workspacePath, string relativePath, string oldContent, string newContent)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));

            if (!fullPath.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("路径越界: {Path} / Path escape detected: {Path}", relativePath, relativePath);
                return false;
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("文件不存在: {Path} / File not found: {Path}", relativePath, relativePath);
                return false;
            }

            var currentContent = File.ReadAllText(fullPath);
            if (!currentContent.Contains(oldContent))
            {
                _logger.LogWarning("未找到要替换的内容: {Path} / Old content not found in file: {Path}", relativePath, relativePath);
                return false;
            }

            var updatedContent = currentContent.Replace(oldContent, newContent);
            File.WriteAllText(fullPath, updatedContent);
            _logger.LogInformation("文件已编辑: {Path} / File edited: {Path}", relativePath, relativePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "编辑文件失败: {Path} / Failed to edit file: {Path}", relativePath, relativePath);
            return false;
        }
    }

    /// <summary>
    /// 读取文件内容 / Read file content
    /// </summary>
    /// <param name="workspacePath">工作空间根路径 / Workspace root path</param>
    /// <param name="relativePath">相对路径 / Relative file path</param>
    /// <returns>文件内容，失败返回null / File content, or null on failure</returns>
    public string? ReadFile(string workspacePath, string relativePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));

            if (!fullPath.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("路径越界: {Path} / Path escape detected: {Path}", relativePath, relativePath);
                return null;
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("文件不存在: {Path} / File not found: {Path}", relativePath, relativePath);
                return null;
            }

            return File.ReadAllText(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取文件失败: {Path} / Failed to read file: {Path}", relativePath, relativePath);
            return null;
        }
    }

    /// <summary>
    /// 列出目录内容 / List directory contents
    /// </summary>
    /// <param name="workspacePath">工作空间根路径 / Workspace root path</param>
    /// <param name="directory">相对目录路径 / Relative directory path</param>
    /// <returns>文件和目录列表 / List of files and directories</returns>
    public string[] ListFiles(string workspacePath, string directory)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(workspacePath, directory));

            if (!fullPath.StartsWith(Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("路径越界: {Path} / Path escape detected: {Path}", directory, directory);
                return [];
            }

            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning("目录不存在: {Path} / Directory not found: {Path}", directory, directory);
                return [];
            }

            var entries = Directory.GetFileSystemEntries(fullPath)
                .Select(e => Path.GetRelativePath(workspacePath, e).Replace('\\', '/'))
                .OrderBy(e => e)
                .ToArray();

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出目录失败: {Path} / Failed to list directory: {Path}", directory, directory);
            return [];
        }
    }
}
