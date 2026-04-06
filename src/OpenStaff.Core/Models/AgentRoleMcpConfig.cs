namespace OpenStaff.Core.Models;

/// <summary>
/// 员工 MCP 绑定 — AgentRole 与 McpServerConfig 的多对多关系
/// </summary>
public class AgentRoleMcpConfig
{
    public Guid AgentRoleId { get; set; }
    public Guid McpServerConfigId { get; set; }
    public string? ToolFilter { get; set; } // JSON: 可选，只启用部分工具 ["tool1", "tool2"]

    // 导航属性
    public AgentRole? AgentRole { get; set; }
    public McpServerConfig? McpServerConfig { get; set; }
}
