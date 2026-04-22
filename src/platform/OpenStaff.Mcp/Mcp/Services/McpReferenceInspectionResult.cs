namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 单个引用检查器返回的阻塞信息。
/// en: Blocking information returned by a single reference inspector.
/// </summary>
public sealed class McpReferenceInspectionResult
{
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];

    public IReadOnlyList<string> ReferencedByConfigs { get; init; } = [];

    public IReadOnlyList<string> ReferencedByProjectBindings { get; init; } = [];

    public IReadOnlyList<string> ReferencedByRoleBindings { get; init; } = [];
}
