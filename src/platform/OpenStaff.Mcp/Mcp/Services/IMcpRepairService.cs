using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: MCP 安装修复接口。
/// en: Contract for MCP repair operations.
/// </summary>
public interface IMcpRepairService
{
    Task<RepairResult> RepairInstallAsync(Guid installId, CancellationToken cancellationToken = default);
}
