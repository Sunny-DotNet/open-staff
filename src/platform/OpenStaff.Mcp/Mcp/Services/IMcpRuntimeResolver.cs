using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: MCP 运行时解析接口。
/// en: Contract for resolving MCP runtime specifications.
/// </summary>
public interface IMcpRuntimeResolver
{
    Task<RuntimeSpec> ResolveRuntimeAsync(Guid installId, CancellationToken cancellationToken = default);
}
