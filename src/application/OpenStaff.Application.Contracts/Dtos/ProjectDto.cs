namespace OpenStaff.Dtos;

/// <summary>
/// 项目摘要信息。
/// Summary information for a project.
/// </summary>
public class ProjectDto
{
    /// <summary>项目唯一标识。 / Unique project identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>项目名称。 / Project name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>项目描述。 / Project description.</summary>
    public string? Description { get; set; }

    /// <summary>项目工作区路径。 / Workspace path for the project.</summary>
    public string? WorkspacePath { get; set; }

    /// <summary>项目当前状态。 / Current project status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>项目所处阶段。 / Current project phase.</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>项目默认语言。 / Default language for the project.</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>默认模型提供商标识。 / Identifier of the default model provider.</summary>
    public Guid? DefaultProviderId { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? DefaultModelName { get; set; }

    /// <summary>扩展配置 JSON。 / Additional configuration JSON.</summary>
    public string? ExtraConfig { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新时间（UTC）。 / Last update time in UTC.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建项目的输入参数。
/// Input used to create a project.
/// </summary>
public class CreateProjectInput
{
    /// <summary>项目名称。 / Project name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>项目描述。 / Project description.</summary>
    public string? Description { get; set; }

    /// <summary>项目默认语言。 / Default language for the project.</summary>
    public string? Language { get; set; }

    /// <summary>默认模型提供商标识。 / Identifier of the default model provider.</summary>
    public Guid? DefaultProviderId { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? DefaultModelName { get; set; }
}

/// <summary>
/// 更新项目的输入参数。
/// Input used to update a project.
/// </summary>
public class UpdateProjectInput
{
    /// <summary>项目名称。 / Project name.</summary>
    public string? Name { get; set; }

    /// <summary>项目描述。 / Project description.</summary>
    public string? Description { get; set; }

    /// <summary>项目默认语言。 / Default language for the project.</summary>
    public string? Language { get; set; }

    /// <summary>默认模型提供商标识。 / Identifier of the default model provider.</summary>
    public Guid? DefaultProviderId { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? DefaultModelName { get; set; }

    /// <summary>扩展配置 JSON。 / Additional configuration JSON.</summary>
    public string? ExtraConfig { get; set; }
}
