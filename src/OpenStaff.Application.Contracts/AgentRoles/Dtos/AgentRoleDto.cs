using OpenStaff.Core.Models;

namespace OpenStaff.Application.Contracts.AgentRoles.Dtos;

public class AgentRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Avatar { get; set; }
    public bool IsBuiltin { get; set; }

    /// <summary>是否为自动发现的虚拟条目（未物化到数据库）</summary>
    public bool IsVirtual { get; set; }

    public AgentSource Source { get; set; }
    public string? ProviderType { get; set; }
    public string? ModelProviderId { get; set; }
    public string? ModelProviderName { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
    public AgentSoulDto? Soul { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AgentSoulDto
{
    public List<string> Traits { get; set; } = [];
    public string? Style { get; set; }
    public List<string> Attitudes { get; set; } = [];
    public string? Custom { get; set; }
}

public class CreateAgentRoleInput
{
    public string Name { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Avatar { get; set; }
    public AgentSource Source { get; set; } = AgentSource.Custom;
    public string? ProviderType { get; set; }
    public string? ModelProviderId { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
    public AgentSoulDto? Soul { get; set; }
}

public class UpdateAgentRoleInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Avatar { get; set; }
    public string? ModelProviderId { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
    public AgentSoulDto? Soul { get; set; }
}

public class TestChatRequest
{
    public Guid AgentRoleId { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 可选的实时配置覆盖，不保存到数据库。
    /// 允许在不修改角色配置的情况下测试不同的提示词/模型等。
    /// </summary>
    public AgentRoleInput? Override { get; set; }
}

/// <summary>
/// 用于临时覆盖角色配置的输入（测试聊天使用）
/// </summary>
public class AgentRoleInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ModelName { get; set; }
    public string? ModelProviderId { get; set; }
    public double? Temperature { get; set; }
    public AgentSoulDto? Soul { get; set; }
}
