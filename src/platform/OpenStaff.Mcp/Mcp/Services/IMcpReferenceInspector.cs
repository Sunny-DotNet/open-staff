using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 宿主可扩展的引用检查接口；模块通过它了解是否仍存在外部绑定。
/// en: Host-extensible reference inspection contract that lets the module discover whether external bindings still exist.
/// </summary>
public interface IMcpReferenceInspector
{
    Task<McpReferenceInspectionResult> InspectAsync(InstalledMcp installedMcp, CancellationToken cancellationToken = default);
}
