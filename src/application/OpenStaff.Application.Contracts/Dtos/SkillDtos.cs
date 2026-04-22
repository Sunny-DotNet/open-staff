using OpenStaff.Entities;

namespace OpenStaff.Dtos;

/// <summary>
/// Skill 目录查询输入。
/// Skill catalog query input.
/// </summary>
public class SkillCatalogQueryInput
{
    /// <summary>搜索关键字。 / Search text.</summary>
    public string? Query { get; set; }

    /// <summary>按 owner 过滤。 / Owner filter.</summary>
    public string? Owner { get; set; }

    /// <summary>按 repo 过滤。 / Repository filter.</summary>
    public string? Repo { get; set; }

    /// <summary>排序字段。 / Sort field.</summary>
    public string SortBy { get; set; } = "installs";

    /// <summary>排序方向。 / Sort direction.</summary>
    public string SortOrder { get; set; } = "desc";

    /// <summary>页码（从 1 开始）。 / Page number starting at 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数。 / Page size.</summary>
    public int PageSize { get; set; } = 24;
}

/// <summary>
/// Skill 目录分页结果。
/// Paginated skill catalog result.
/// </summary>
public class SkillCatalogPageDto
{
    /// <summary>结果项。 / Result items.</summary>
    public List<SkillCatalogItemDto> Items { get; set; } = [];

    /// <summary>总条数。 / Total item count.</summary>
    public int Total { get; set; }

    /// <summary>页码。 / Page number.</summary>
    public int Page { get; set; }

    /// <summary>每页条数。 / Page size.</summary>
    public int PageSize { get; set; }

    /// <summary>总页数。 / Total page count.</summary>
    public int TotalPages { get; set; }

    /// <summary>抓取时间（UTC）。 / Catalog scraped timestamp in UTC.</summary>
    public DateTimeOffset? ScrapedAt { get; set; }
}

/// <summary>
/// Skill 目录项。
/// Skill catalog item.
/// </summary>
public class SkillCatalogItemDto
{
    /// <summary>数据源标识。 / Catalog source key.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>源仓库 owner/repo。 / Source repository in owner/repo form.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Skill 标识。 / Skill identifier.</summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>技能名称。 / Skill name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示名称。 / Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>描述。 / Description.</summary>
    public string? Description { get; set; }

    /// <summary>安装次数。 / Install count.</summary>
    public int Installs { get; set; }

    /// <summary>仓库 owner。 / Repository owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>仓库名。 / Repository name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>GitHub 仓库地址。 / GitHub repository URL.</summary>
    public string? GithubUrl { get; set; }

    /// <summary>是否已经安装。 / Whether the skill is installed.</summary>
    public bool IsInstalled { get; set; }
}

/// <summary>
/// Skill 来源聚合项。
/// Aggregated skill source item.
/// </summary>
public class SkillCatalogSourceDto
{
    /// <summary>数据源标识。 / Source key.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>展示名称。 / Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>源仓库 owner/repo。 / Source repository in owner/repo form.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>仓库 owner。 / Repository owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>仓库名。 / Repository name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Skill 数量。 / Number of skills.</summary>
    public int SkillCount { get; set; }

    /// <summary>总安装量。 / Total installs.</summary>
    public int TotalInstalls { get; set; }
}

/// <summary>
/// 已安装 Skill 过滤输入。
/// Installed skill filter input.
/// </summary>
public class GetInstalledSkillsInput
{
    /// <summary>搜索关键字。 / Search text.</summary>
    public string? Query { get; set; }
}

/// <summary>
/// 已安装 Skill 记录。
/// Installed skill record DTO.
/// </summary>
public class InstalledSkillDto
{
    /// <summary>唯一标识。 / Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>安装键。 / Stable installation key.</summary>
    public string InstallKey { get; set; } = string.Empty;

    /// <summary>数据源标识。 / Catalog source key.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>安装范围。 / Installation scope.</summary>
    public string Scope { get; set; } = SkillScopes.Global;

    /// <summary>关联项目标识。 / Associated project identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>项目名称。 / Project name.</summary>
    public string? ProjectName { get; set; }

    /// <summary>源仓库 owner/repo。 / Source repository in owner/repo form.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>仓库 owner。 / Repository owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>仓库名。 / Repository name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Skill 标识。 / Skill identifier.</summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>技能名称。 / Skill name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示名称。 / Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>描述。 / Description.</summary>
    public string? Description { get; set; }

    /// <summary>GitHub 仓库地址。 / GitHub repository URL.</summary>
    public string? GithubUrl { get; set; }

    /// <summary>安装次数快照。 / Install count snapshot.</summary>
    public int Installs { get; set; }

    /// <summary>安装模式。 / Installation mode or legacy source marker.</summary>
    public string InstallMode { get; set; } = SkillInstallModes.Managed;

    /// <summary>目标 agent 列表。 / Target agent list from legacy records.</summary>
    public List<string> TargetAgents { get; set; } = [];

    /// <summary>安装根目录。 / Installation root path.</summary>
    public string InstallRootPath { get; set; } = string.Empty;

    /// <summary>是否启用。 / Whether the record is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>安装状态。 / Installation status.</summary>
    public string Status { get; set; } = SkillInstallStatuses.Installed;

    /// <summary>安装状态说明。 / Human-readable installation status message.</summary>
    public string? StatusMessage { get; set; }

    /// <summary>是否由新文件系统仓库管理。 / Whether the item is managed by the new filesystem store.</summary>
    public bool IsManaged { get; set; } = true;

    /// <summary>来源提交快照。 / Source revision snapshot.</summary>
    public string? SourceRevision { get; set; }

    /// <summary>创建时间（UTC）。 / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新时间（UTC）。 / Update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Skill 安装输入。
/// Skill installation input.
/// </summary>
public class InstallSkillInput
{
    /// <summary>数据源标识。 / Catalog source key.</summary>
    public string SourceKey { get; set; } = SkillSourceKeys.SkillsSh;

    /// <summary>仓库 owner。 / Repository owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>仓库名。 / Repository name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Skill 标识。 / Skill identifier.</summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>覆盖已有目录。 / Whether to overwrite an existing managed install.</summary>
    public bool OverwriteExisting { get; set; } = true;
}

/// <summary>
/// Skill 卸载输入。
/// Skill uninstall input.
/// </summary>
public class UninstallSkillInput
{
    /// <summary>仓库 owner。 / Repository owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>仓库名。 / Repository name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Skill 标识。 / Skill identifier.</summary>
    public string SkillId { get; set; } = string.Empty;
}

/// <summary>
/// Skill 维护输入。
/// Skill maintenance input.
/// </summary>
public class SkillMaintenanceInput
{
    /// <summary>旧版作用域字段，当前仅为兼容保留。 / Legacy scope field kept for compatibility.</summary>
    public string Scope { get; set; } = SkillScopes.Global;

    /// <summary>旧版项目字段，当前仅为兼容保留。 / Legacy project field kept for compatibility.</summary>
    public Guid? ProjectId { get; set; }
}

/// <summary>
/// Skill 维护结果。
/// Skill maintenance command result.
/// </summary>
public class SkillMaintenanceResultDto
{
    /// <summary>执行是否成功。 / Whether the command succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>执行的命令行。 / Executed command line.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>标准输出。 / Standard output.</summary>
    public string? StandardOutput { get; set; }

    /// <summary>标准错误。 / Standard error.</summary>
    public string? StandardError { get; set; }

    /// <summary>工作目录。 / Working directory.</summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>检查过的技能数量。 / Number of checked skills.</summary>
    public int CheckedCount { get; set; }

    /// <summary>发现可更新的技能数量。 / Number of outdated skills detected.</summary>
    public int OutdatedCount { get; set; }

    /// <summary>已更新的技能数量。 / Number of updated skills.</summary>
    public int UpdatedCount { get; set; }
}

/// <summary>
/// 测试场景与已安装 skill 之间的绑定信息。
/// Binding information between an agent-role test chat and an installed skill.
/// </summary>
public class AgentRoleSkillBindingDto
{
    public Guid Id { get; set; }
    public Guid AgentRoleId { get; set; }
    public string SkillInstallKey { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? GithubUrl { get; set; }
    public bool IsEnabled { get; set; }
    public string ResolutionStatus { get; set; } = SkillBindingResolutionStatuses.Resolved;
    public string? ResolutionMessage { get; set; }
    public string? InstallRootPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 替换测试场景 skill 绑定时使用的输入。
/// Input used when replacing skill bindings for a role-test chat.
/// </summary>
public class AgentRoleSkillBindingInput
{
    public string SkillInstallKey { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? GithubUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 替换测试场景 skill 绑定请求。
/// Request used to replace skill bindings for a role-test chat.
/// </summary>
public class ReplaceAgentRoleSkillBindingsRequest
{
    public Guid AgentRoleId { get; set; }
    public List<AgentRoleSkillBindingInput> Bindings { get; set; } = [];
}

/// <summary>
/// 项目成员与已安装 skill 之间的绑定信息。
/// Binding information between a project agent and an installed skill.
/// </summary>
public class ProjectAgentSkillBindingDto
{
    public Guid Id { get; set; }
    public Guid ProjectAgentRoleId { get; set; }
    public string SkillInstallKey { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? GithubUrl { get; set; }
    public bool IsEnabled { get; set; }
    public string ResolutionStatus { get; set; } = SkillBindingResolutionStatuses.Resolved;
    public string? ResolutionMessage { get; set; }
    public string? InstallRootPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 替换项目成员 skill 绑定时使用的输入。
/// Input used when replacing skill bindings for a project agent.
/// </summary>
public class ProjectAgentSkillBindingInput
{
    public string SkillInstallKey { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? GithubUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 替换项目成员 skill 绑定请求。
/// Request used to replace skill bindings for a project agent.
/// </summary>
public class ReplaceProjectAgentSkillBindingsRequest
{
    public Guid ProjectAgentRoleId { get; set; }
    public List<ProjectAgentSkillBindingInput> Bindings { get; set; } = [];
}
