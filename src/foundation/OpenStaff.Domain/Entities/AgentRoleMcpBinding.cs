namespace OpenStaff.Entities;

/// <summary>
/// 角色测试场景下对 MCP server 的实际使用绑定 / Actual usage binding between an agent role and an installed MCP server for test chat.
/// </summary>
public class AgentRoleMcpBinding:EntityBase<Guid>
{
    /// <summary>角色标识 / Agent-role identifier.</summary>
    public Guid AgentRoleId { get; set; }

    /// <summary>MCP 服务器标识 / Installed MCP server identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>工具过滤 JSON，仅启用部分工具 / Optional tool-filter JSON used to enable only selected tools.</summary>
    public string? ToolFilter { get; set; }

    /// <summary>是否启用 / Whether the binding is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联角色 / Linked agent role.</summary>
    public AgentRole? AgentRole { get; set; }

    /// <summary>关联 MCP 服务器 / Linked MCP server.</summary>
    public McpServer? McpServer { get; set; }
}
