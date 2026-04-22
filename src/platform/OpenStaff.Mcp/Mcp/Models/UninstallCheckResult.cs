namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示卸载前检查结果。
/// en: Represents the result of an uninstall pre-check.
/// </summary>
public sealed class UninstallCheckResult
{
    public bool CanUninstall { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = [];

    public IReadOnlyList<string> ReferencedByConfigs { get; init; } = [];

    public IReadOnlyList<string> ReferencedByProjectBindings { get; init; } = [];

    public IReadOnlyList<string> ReferencedByRoleBindings { get; init; } = [];
}
