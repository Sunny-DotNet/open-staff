namespace OpenStaff.Entities;

/// <summary>
/// 已安装 Skill 记录。
/// Installed skill record.
/// </summary>
public class InstalledSkill:EntityBase<Guid>
{
    /// <summary>唯一安装键。 / Unique installation key.</summary>
    public string InstallKey { get; set; } = string.Empty;

    /// <summary>数据源标识。 / Catalog source key.</summary>
    public string SourceKey { get; set; } = SkillSourceKeys.SkillsSh;

    /// <summary>安装范围。 / Installation scope.</summary>
    public string Scope { get; set; } = SkillScopes.Project;

    /// <summary>关联项目标识。 / Associated project identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>关联项目。 / Associated project.</summary>
    public Project? Project { get; set; }

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

    /// <summary>GitHub 仓库地址。 / GitHub repository URL.</summary>
    public string? GithubUrl { get; set; }

    /// <summary>安装次数快照。 / Install count snapshot from the catalog.</summary>
    public int Installs { get; set; }

    /// <summary>安装模式。 / Installation mode.</summary>
    public string InstallMode { get; set; } = SkillInstallModes.Symlink;

    /// <summary>目标 agent 列表 JSON。 / JSON payload for target agents.</summary>
    public string TargetAgentsJson { get; set; } = "[]";

    /// <summary>安装根目录。 / Installation root path.</summary>
    public string InstallRootPath { get; set; } = string.Empty;

    /// <summary>原始元数据 JSON。 / Raw metadata JSON.</summary>
    public string? RawMetadataJson { get; set; }

    /// <summary>是否启用。 / Whether the record is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间（UTC）。 / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间（UTC）。 / Update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Skill 数据源常量。
/// Well-known skill source keys.
/// </summary>
public static class SkillSourceKeys
{
    public const string SkillsSh = "skills.sh";
}

/// <summary>
/// Skill 安装范围常量。
/// Well-known skill installation scopes.
/// </summary>
public static class SkillScopes
{
    public const string Project = "project";
    public const string Global = "global";
}

/// <summary>
/// Skill 安装模式常量。
/// Well-known skill installation modes.
/// </summary>
public static class SkillInstallModes
{
    public const string Symlink = "symlink";
    public const string Copy = "copy";
    public const string Managed = "managed";
    public const string Legacy = "legacy";
}

/// <summary>
/// Skill 安装状态常量。
/// Well-known skill installation statuses.
/// </summary>
public static class SkillInstallStatuses
{
    public const string Installed = "installed";
    public const string Missing = "missing";
    public const string Legacy = "legacy";
}

/// <summary>
/// Skill 绑定解析状态常量。
/// Well-known skill binding resolution statuses.
/// </summary>
public static class SkillBindingResolutionStatuses
{
    public const string Resolved = "resolved";
    public const string Missing = "missing";
}
