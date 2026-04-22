using ModelContextProtocol.Client;
using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 基于官方 ModelContextProtocol SDK 创建 MCP 客户端。
/// en: Creates MCP clients using the official ModelContextProtocol SDK.
/// </summary>
public interface IMcpClientFactory
{
    Task<McpClient> CreateAsync(RuntimeSpec runtimeSpec, string? clientName = null, CancellationToken cancellationToken = default);

    Task<McpClient> CreateForInstallAsync(Guid installId, string? clientName = null, CancellationToken cancellationToken = default);
}
