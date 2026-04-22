using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// MCP 管理总入口契约。
/// Unified MCP application contract that covers catalog, install, servers, configs, and bindings.
/// </summary>
public interface IMcpApiService : IApiServiceBase
{
    /// <summary>
    /// 获取所有已注册的 MCP 目录源。
    /// Gets all registered MCP catalog sources.
    /// </summary>
    Task<List<McpSourceDto>> GetSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 搜索 MCP 目录条目。
    /// Searches MCP catalog entries.
    /// </summary>
    Task<McpCatalogSearchResultDto> SearchCatalogAsync(McpCatalogSearchQueryDto query, CancellationToken ct = default);

    /// <summary>
    /// 获取单个 MCP 目录条目详情。
    /// Gets a single MCP catalog entry.
    /// </summary>
    Task<McpCatalogEntryDto?> GetCatalogEntryAsync(string sourceKey, string entryId, CancellationToken ct = default);

    /// <summary>
    /// 从 MCP 目录安装一个服务。
    /// Installs a server from the MCP catalog.
    /// </summary>
    Task<McpServerDto> InstallAsync(InstallMcpServerInput input, CancellationToken ct = default);

    /// <summary>
    /// 检查服务删除/卸载是否会被阻塞。
    /// Checks whether deleting or uninstalling a server would be blocked.
    /// </summary>
    Task<McpUninstallCheckResultDto> CheckUninstallAsync(Guid serverId, CancellationToken ct = default);

    /// <summary>
    /// 获取 MCP 服务定义列表。
    /// Gets the list of MCP server definitions.
    /// </summary>
    Task<List<McpServerDto>> GetAllServersAsync(GetAllServersRequest request, CancellationToken ct = default);

    /// <summary>
    /// 根据标识获取 MCP 服务定义。
    /// Gets an MCP server definition by identifier.
    /// </summary>
    Task<McpServerDto?> GetServerByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 创建自定义 MCP 服务定义。
    /// Creates a custom MCP server definition.
    /// </summary>
    Task<McpServerDto> CreateCustomServerAsync(CreateMcpServerInput input, CancellationToken ct = default);

    /// <summary>
    /// 更新 MCP 服务定义。
    /// Updates an MCP server definition.
    /// </summary>
    Task<McpServerDto?> UpdateServerAsync(Guid id, UpdateMcpServerInput input, CancellationToken ct = default);

    /// <summary>
    /// 删除或卸载 MCP 服务定义。
    /// Deletes or uninstalls an MCP server definition.
    /// </summary>
    Task<DeleteMcpServerResultDto> DeleteServerAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 修复受管安装的 MCP 服务。
    /// Repairs a managed MCP installation.
    /// </summary>
    Task<McpRepairResultDto> RepairInstallAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 设置 MCP 服务定义启用状态。
    /// Sets the enabled state of an MCP server definition.
    /// </summary>
    Task SetServerEnabledAsync(Guid id, bool isEnabled, CancellationToken ct = default);

    /// <summary>
    /// 获取某个 MCP 服务定义下的配置实例。
    /// Gets configuration instances that belong to an MCP server definition.
    /// </summary>
    Task<List<McpServerConfigDto>> GetConfigsByServerAsync(Guid mcpServerId, CancellationToken ct = default);

    /// <summary>
    /// 获取全部 MCP 配置实例。
    /// Gets all MCP server configuration instances.
    /// </summary>
    Task<List<McpServerConfigDto>> GetAllConfigsAsync(CancellationToken ct = default);

    /// <summary>
    /// 根据标识获取配置实例。
    /// Gets an MCP configuration instance by identifier.
    /// </summary>
    Task<McpServerConfigDto?> GetConfigByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 创建 MCP 配置实例。
    /// Creates an MCP configuration instance.
    /// </summary>
    Task<McpServerConfigDto> CreateConfigAsync(CreateMcpServerConfigInput input, CancellationToken ct = default);

    /// <summary>
    /// 更新 MCP 配置实例。
    /// Updates an MCP configuration instance.
    /// </summary>
    Task<McpServerConfigDto?> UpdateConfigAsync(Guid id, UpdateMcpServerConfigInput input, CancellationToken ct = default);

    /// <summary>
    /// 删除 MCP 配置实例。
    /// Deletes an MCP configuration instance.
    /// </summary>
    Task<bool> DeleteConfigAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 测试指定 MCP 配置的连通性并返回可用工具。
    /// Tests connectivity for a configuration and returns the discovered tools.
    /// </summary>
    Task<TestMcpConnectionResult> TestConnectionAsync(Guid configId, CancellationToken ct = default);

    /// <summary>
    /// 使用草稿配置测试 MCP 连通性并返回可用工具。
    /// Tests MCP connectivity using a draft configuration and returns discovered tools.
    /// </summary>
    Task<TestMcpConnectionResult> TestConnectionDraftAsync(TestMcpConnectionDraftInput input, CancellationToken ct = default);

    /// <summary>
    /// 获取角色测试场景绑定的 MCP 服务器列表。
    /// Gets the MCP servers bound to an agent-role test chat.
    /// </summary>
    Task<List<AgentRoleMcpBindingDto>> GetAgentRoleBindingsAsync(Guid agentRoleId, CancellationToken ct = default);

    /// <summary>
    /// 用新的绑定列表替换角色测试场景当前的 MCP 绑定。
    /// Replaces the current MCP bindings for an agent-role test chat.
    /// </summary>
    Task ReplaceAgentRoleBindingsAsync(ReplaceAgentRoleMcpBindingsRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取项目智能体绑定的 MCP 服务器列表。
    /// Gets the MCP servers bound to a project agent.
    /// </summary>
    Task<List<ProjectAgentMcpBindingDto>> GetProjectAgentBindingsAsync(Guid projectAgentId, CancellationToken ct = default);

    /// <summary>
    /// 用新的绑定列表替换项目智能体当前的 MCP 绑定。
    /// Replaces the current MCP bindings for a project agent.
    /// </summary>
    Task ReplaceProjectAgentBindingsAsync(ReplaceProjectAgentMcpBindingsRequest request, CancellationToken ct = default);

    /// <summary>
    /// 生成指定场景下新增 MCP 绑定时使用的默认草稿。
    /// Generates the default draft used when adding an MCP binding for a specific scope.
    /// </summary>
    Task<McpBindingDraftDto> CreateBindingDraftAsync(CreateMcpBindingDraftInput input, CancellationToken ct = default);
}

/// <summary>
/// 兼容旧名称的 MCP 服务契约。
/// Backward-compatible alias for the legacy MCP server contract name.
/// </summary>
public interface IMcpServerApiService : IMcpApiService
{
}


