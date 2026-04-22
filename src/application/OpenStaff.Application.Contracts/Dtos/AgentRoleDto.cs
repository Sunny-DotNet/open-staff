using OpenStaff.Entities;

namespace OpenStaff.Dtos;

public class AgentRoleQueryInput : IPagingInput
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = int.MaxValue;
}

/// <summary>
/// 智能体角色摘要信息。
/// Summary information for an agent role.
/// </summary>
public class AgentRoleDto
{
    /// <summary>角色唯一标识。 / Unique role identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>角色名称。 / Role name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色类型键。 / Role type key.</summary>
    public string RoleType { get; set; } = string.Empty;

    /// <summary>角色描述。 / Role description.</summary>
    public string? Description { get; set; }

    /// <summary>职位名称。 / Job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>系统提示词。 / System prompt.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>头像或头像数据。 / Avatar or avatar payload.</summary>
    public string? Avatar { get; set; }

    /// <summary>是否为内置角色。 / Whether the role is built in.</summary>
    public bool IsBuiltin { get; set; }

    /// <summary>是否为自动发现但未落库的虚拟条目。 / Whether the role is an auto-discovered virtual entry that is not persisted.</summary>
    public bool IsVirtual { get; set; }

    /// <summary>角色来源，常见值为 Custom、Builtin 或 Vendor。 / Role source, commonly Custom, Builtin, or Vendor.</summary>
    public AgentSource Source { get; set; }

    /// <summary>供应商类型键。 / Vendor provider type key.</summary>
    public string? ProviderType { get; set; }

    /// <summary>模型提供商标识。 / Model provider identifier.</summary>
    public string? ModelProviderId { get; set; }

    /// <summary>模型提供商名称。 / Model provider display name.</summary>
    public string? ModelProviderName { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? ModelName { get; set; }

    /// <summary>角色配置 JSON。 / Role configuration JSON.</summary>
    public string? Config { get; set; }

    /// <summary>灵魂画像配置。 / Personality profile configuration.</summary>
    public AgentSoulDto? Soul { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 智能体灵魂画像配置。
/// Personality profile configuration for an agent role.
/// </summary>
public class AgentSoulDto
{
    /// <summary>性格特征列表。 / Personality traits.</summary>
    public List<string> Traits { get; set; } = [];

    /// <summary>表达风格。 / Communication style.</summary>
    public string? Style { get; set; }

    /// <summary>态度或立场列表。 / Attitudes or stances.</summary>
    public List<string> Attitudes { get; set; } = [];

    /// <summary>自由扩展描述。 / Free-form custom description.</summary>
    public string? Custom { get; set; }
}

/// <summary>
/// 创建角色的输入参数。
/// Input used to create an agent role.
/// </summary>
public class CreateAgentRoleInput
{
    /// <summary>角色名称。 / Role name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色类型键。 / Role type key.</summary>
    public string RoleType { get; set; } = string.Empty;

    /// <summary>角色描述。 / Role description.</summary>
    public string? Description { get; set; }

    /// <summary>职位名称。 / Job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>系统提示词。 / System prompt.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>头像或头像数据。 / Avatar or avatar payload.</summary>
    public string? Avatar { get; set; }

    /// <summary>角色来源。 / Role source.</summary>
    public AgentSource Source { get; set; } = AgentSource.Custom;

    /// <summary>供应商类型键。 / Vendor provider type key.</summary>
    public string? ProviderType { get; set; }

    /// <summary>模型提供商标识。 / Model provider identifier.</summary>
    public string? ModelProviderId { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? ModelName { get; set; }

    /// <summary>角色配置 JSON。 / Role configuration JSON.</summary>
    public string? Config { get; set; }

    /// <summary>灵魂画像配置。 / Personality profile configuration.</summary>
    public AgentSoulDto? Soul { get; set; }
}

/// <summary>
/// 更新角色的输入参数。
/// Input used to update an agent role.
/// </summary>
public class UpdateAgentRoleInput
{
    /// <summary>角色名称。 / Role name.</summary>
    public string? Name { get; set; }

    /// <summary>角色描述。 / Role description.</summary>
    public string? Description { get; set; }

    /// <summary>职位名称。 / Job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>系统提示词。 / System prompt.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>头像或头像数据。 / Avatar or avatar payload.</summary>
    public string? Avatar { get; set; }

    /// <summary>模型提供商标识。 / Model provider identifier.</summary>
    public string? ModelProviderId { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? ModelName { get; set; }

    /// <summary>角色配置 JSON。 / Role configuration JSON.</summary>
    public string? Config { get; set; }

    /// <summary>灵魂画像配置。 / Personality profile configuration.</summary>
    public AgentSoulDto? Soul { get; set; }
}

/// <summary>
/// 临时测试角色聊天的请求。
/// Request used to test-chat with an agent role.
/// </summary>
public class TestChatRequest
{
    /// <summary>角色标识。 / Agent role identifier.</summary>
    public Guid AgentRoleId { get; set; }

    /// <summary>测试消息。 / Test message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>仅用于当前测试的临时覆盖配置，不会持久化。 / Temporary override configuration for the current test only; it is not persisted.</summary>
    public AgentRoleInput? Override { get; set; }
}

/// <summary>
/// 测试聊天时使用的角色临时覆盖输入。
/// Temporary role override used during test chat.
/// </summary>
public class AgentRoleInput
{
    /// <summary>角色名称。 / Role name.</summary>
    public string? Name { get; set; }

    /// <summary>角色描述。 / Role description.</summary>
    public string? Description { get; set; }

    /// <summary>模型名称。 / Model name.</summary>
    public string? ModelName { get; set; }

    /// <summary>模型提供商标识。 / Model provider identifier.</summary>
    public string? ModelProviderId { get; set; }

    /// <summary>采样温度。 / Sampling temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>灵魂画像配置。 / Personality profile configuration.</summary>
    public AgentSoulDto? Soul { get; set; }
}

/// <summary>
/// 预览角色模板导入的输入。
/// Input used to preview a role-template import.
/// </summary>
public class PreviewAgentRoleTemplateImportInput
{
    /// <summary>角色模板文件内容。 / Raw role-template document content.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 正式导入角色模板的输入。
/// Input used to import a role template into the local role library.
/// </summary>
public class ImportAgentRoleTemplateInput
{
    /// <summary>角色模板文件内容。 / Raw role-template document content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>若已存在同名角色，是否允许覆盖。 / Whether an existing role with the same identity can be updated.</summary>
    public bool OverwriteExisting { get; set; } = true;
}

/// <summary>
/// 角色模板预览结果。
/// Preview payload returned after parsing a role template.
/// </summary>
public class AgentRoleTemplatePreviewDto
{
    /// <summary>模板原始标识。 / Raw template identifier from the source document.</summary>
    public string? ExternalId { get; set; }

    /// <summary>角色名称。 / Role name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>职位名称。 / Job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>角色描述。 / Role description.</summary>
    public string? Description { get; set; }

    /// <summary>头像。 / Avatar payload.</summary>
    public string? Avatar { get; set; }

    /// <summary>模型名称。 / Model name.</summary>
    public string? ModelName { get; set; }

    /// <summary>模型配置原文。 / Raw model configuration payload.</summary>
    public string? ModelConfig { get; set; }

    /// <summary>灵魂画像。 / Personality profile.</summary>
    public AgentSoulDto? Soul { get; set; }
}

/// <summary>
/// 角色模板中声明的 MCP 依赖。
/// MCP requirement declared by a role template.
/// </summary>
public class AgentRoleTemplateMcpRequirementDto
{
    /// <summary>模板内声明的稳定键。 / Stable key declared by the template.</summary>
    public string? Key { get; set; }

    /// <summary>模板内声明的展示名。 / Display name declared by the template.</summary>
    public string? Name { get; set; }

    /// <summary>模板内声明的 npm 包标识。 / npm package identifier declared by the template.</summary>
    public string? NpmPackage { get; set; }

    /// <summary>模板内声明的 PyPI 包标识。 / PyPI package identifier declared by the template.</summary>
    public string? PypiPackage { get; set; }

    /// <summary>解析状态。 / Resolution status.</summary>
    public string Status { get; set; } = AgentRoleTemplateResolutionStatuses.Missing;

    /// <summary>匹配策略。 / Matching strategy used for resolution.</summary>
    public string? MatchStrategy { get; set; }

    /// <summary>解析说明。 / Human-readable resolution message.</summary>
    public string? Message { get; set; }

    /// <summary>匹配到的本地 MCP 服务标识。 / Matched local MCP server identifier.</summary>
    public Guid? MatchedServerId { get; set; }

    /// <summary>匹配到的本地 MCP 服务名称。 / Matched local MCP server name.</summary>
    public string? MatchedServerName { get; set; }

    /// <summary>匹配到的本地 MCP 服务来源。 / Matched local MCP server source.</summary>
    public string? MatchedServerSource { get; set; }

    /// <summary>匹配到的本地 MCP 配置数量。 / Number of saved configs on the matched server.</summary>
    public int ConfigCount { get; set; }
}

/// <summary>
/// 角色模板中声明的 Skill 依赖。
/// Skill requirement declared by a role template.
/// </summary>
public class AgentRoleTemplateSkillRequirementDto
{
    /// <summary>模板内声明的稳定键。 / Stable key declared by the template.</summary>
    public string? Key { get; set; }

    /// <summary>模板内声明的来源字符串。 / Raw source string declared by the template.</summary>
    public string? Source { get; set; }

    /// <summary>数据源标识。 / Catalog source key.</summary>
    public string? SourceKey { get; set; }

    /// <summary>仓库 owner。 / Repository owner.</summary>
    public string? Owner { get; set; }

    /// <summary>仓库名。 / Repository name.</summary>
    public string? Repo { get; set; }

    /// <summary>Skill 标识。 / Skill identifier.</summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>解析状态。 / Resolution status.</summary>
    public string Status { get; set; } = AgentRoleTemplateResolutionStatuses.Missing;

    /// <summary>匹配策略。 / Matching strategy used for resolution.</summary>
    public string? MatchStrategy { get; set; }

    /// <summary>解析说明。 / Human-readable resolution message.</summary>
    public string? Message { get; set; }

    /// <summary>匹配到的安装键。 / Matched managed-skill install key.</summary>
    public string? InstallKey { get; set; }

    /// <summary>匹配到的技能名称。 / Matched skill display name.</summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// 角色模板导入预览结果。
/// Preview result returned for a role-template import.
/// </summary>
public class PreviewAgentRoleTemplateImportResultDto
{
    /// <summary>角色模板基础资料。 / Parsed role-template metadata.</summary>
    public AgentRoleTemplatePreviewDto Role { get; set; } = new();

    /// <summary>MCP 依赖解析结果。 / MCP requirement resolution results.</summary>
    public List<AgentRoleTemplateMcpRequirementDto> Mcps { get; set; } = [];

    /// <summary>Skill 依赖解析结果。 / Skill requirement resolution results.</summary>
    public List<AgentRoleTemplateSkillRequirementDto> Skills { get; set; } = [];
}

/// <summary>
/// 角色模板正式导入结果。
/// Result returned after importing a role template.
/// </summary>
public class ImportAgentRoleTemplateResultDto
{
    /// <summary>落库后的角色。 / Persisted role produced by the import.</summary>
    public AgentRoleDto Role { get; set; } = new();

    /// <summary>导入时的预览快照。 / Preview snapshot generated during the import.</summary>
    public PreviewAgentRoleTemplateImportResultDto Preview { get; set; } = new();

    /// <summary>新增的 MCP 默认绑定数量。 / Number of added role-level MCP bindings.</summary>
    public int AddedMcpBindings { get; set; }

    /// <summary>新增的 Skill 默认绑定数量。 / Number of added role-level skill bindings.</summary>
    public int AddedSkillBindings { get; set; }
}

/// <summary>
/// 角色模板能力依赖的解析状态常量。
/// Well-known resolution-status constants for role-template capabilities.
/// </summary>
public static class AgentRoleTemplateResolutionStatuses
{
    public const string Resolved = "resolved";
    public const string Missing = "missing";
}
