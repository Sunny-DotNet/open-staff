namespace OpenStaff.Core.Models;

/// <summary>
/// MCP 配置实例 — 一个 McpServer 可以有多个配置实例（如不同 token）
/// </summary>
public class McpServerConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid McpServerId { get; set; } // FK → McpServer
    public string Name { get; set; } = string.Empty; // 配置名称（如 "团队A-GitHub"）
    public string? Description { get; set; }
    public string TransportType { get; set; } = "stdio"; // 继承或覆盖 McpServer 的传输类型
    public string? ConnectionConfig { get; set; } // JSON: { command, args[], url, headers{} }
    public string? EnvironmentVariables { get; set; } // JSON: { "KEY": "value" } — 加密存储
    public string? AuthConfig { get; set; } // JSON: 加密的认证信息
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public McpServer? McpServer { get; set; }
    public ICollection<AgentRoleMcpConfig> AgentBindings { get; set; } = new List<AgentRoleMcpConfig>();
}
