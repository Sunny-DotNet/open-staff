using OpenStaff.Application.Contracts.McpServers.Dtos;

namespace OpenStaff.Application.Contracts.McpServers;

public interface IMcpServerAppService
{
    // MCP Server 定义（市场）
    Task<List<McpServerDto>> GetAllServersAsync(string? category = null, string? search = null, CancellationToken ct = default);
    Task<McpServerDto?> GetServerByIdAsync(Guid id, CancellationToken ct = default);
    Task<McpServerDto> CreateServerAsync(CreateMcpServerInput input, CancellationToken ct = default);
    Task<bool> DeleteServerAsync(Guid id, CancellationToken ct = default);

    // MCP 配置实例
    Task<List<McpServerConfigDto>> GetConfigsByServerAsync(Guid mcpServerId, CancellationToken ct = default);
    Task<List<McpServerConfigDto>> GetAllConfigsAsync(CancellationToken ct = default);
    Task<McpServerConfigDto?> GetConfigByIdAsync(Guid id, CancellationToken ct = default);
    Task<McpServerConfigDto> CreateConfigAsync(CreateMcpServerConfigInput input, CancellationToken ct = default);
    Task<McpServerConfigDto?> UpdateConfigAsync(Guid id, UpdateMcpServerConfigInput input, CancellationToken ct = default);
    Task<bool> DeleteConfigAsync(Guid id, CancellationToken ct = default);

    // 测试连接
    Task<TestMcpConnectionResult> TestConnectionAsync(Guid configId, CancellationToken ct = default);

    // 员工 MCP 绑定
    Task<List<AgentMcpBindingDto>> GetAgentBindingsAsync(Guid agentRoleId, CancellationToken ct = default);
    Task SetAgentBindingsAsync(Guid agentRoleId, List<Guid> mcpServerConfigIds, CancellationToken ct = default);
}
