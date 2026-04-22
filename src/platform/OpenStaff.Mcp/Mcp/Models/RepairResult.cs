namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示安装修复操作的结果。
/// en: Represents the outcome of a repair operation.
/// </summary>
public sealed class RepairResult
{
    public InstalledMcp InstalledMcp { get; init; } = new();

    public bool Repaired { get; init; }

    public string? Message { get; init; }
}
