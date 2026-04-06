namespace OpenStaff.Application.Contracts.McpServers.Dtos;

public class McpServerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Category { get; set; } = "general";
    public string TransportType { get; set; } = "stdio";
    public string Source { get; set; } = "builtin";
    public string? DefaultConfig { get; set; }
    public string? Homepage { get; set; }
    public string? NpmPackage { get; set; }
    public string? PypiPackage { get; set; }
    public bool IsEnabled { get; set; }
    public int ConfigCount { get; set; } // 已有配置实例数
}

public class McpServerConfigDto
{
    public Guid Id { get; set; }
    public Guid McpServerId { get; set; }
    public string McpServerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TransportType { get; set; } = "stdio";
    public string? ConnectionConfig { get; set; }
    public string? EnvironmentVariables { get; set; } // 返回时脱敏
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMcpServerInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Category { get; set; } = "general";
    public string TransportType { get; set; } = "stdio";
    public string? DefaultConfig { get; set; }
    public string? Homepage { get; set; }
    public string? NpmPackage { get; set; }
    public string? PypiPackage { get; set; }
}

public class CreateMcpServerConfigInput
{
    public Guid McpServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TransportType { get; set; } = "stdio";
    public string? ConnectionConfig { get; set; } // JSON
    public string? EnvironmentVariables { get; set; } // JSON
    public string? AuthConfig { get; set; } // JSON — 会被加密存储
}

public class UpdateMcpServerConfigInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? TransportType { get; set; }
    public string? ConnectionConfig { get; set; }
    public string? EnvironmentVariables { get; set; }
    public string? AuthConfig { get; set; }
    public bool? IsEnabled { get; set; }
}

public class McpToolDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? InputSchema { get; set; }
}

public class TestMcpConnectionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<McpToolDto> Tools { get; set; } = [];
}

public class AgentMcpBindingDto
{
    public Guid McpServerConfigId { get; set; }
    public string McpServerConfigName { get; set; } = string.Empty;
    public string McpServerName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ToolFilter { get; set; }
}

public class GetAllServersRequest
{
    public string? Category { get; set; }
    public string? Search { get; set; }
}

public class SetAgentBindingsRequest
{
    public Guid AgentRoleId { get; set; }
    public List<Guid> McpServerConfigIds { get; set; } = [];
}

public class CreateAgentBindingInput
{
    public Guid AgentRoleId { get; set; }
    public Guid McpServerConfigId { get; set; }
    public string? ToolFilter { get; set; }
}
