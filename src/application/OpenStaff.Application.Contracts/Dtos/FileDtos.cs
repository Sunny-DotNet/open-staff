namespace OpenStaff.Dtos;

/// <summary>
/// 项目文件树节点。
/// Node within a project file tree.
/// </summary>
public class FileNodeDto
{
    /// <summary>文件或目录名称。 / File or directory name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>相对项目根目录的路径。 / Path relative to the project root.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>是否为目录。 / Whether the node represents a directory.</summary>
    public bool IsDirectory { get; set; }

    /// <summary>子节点集合。 / Child nodes.</summary>
    public List<FileNodeDto>? Children { get; set; }
}

/// <summary>
/// 项目检查点摘要。
/// Summary information for a project checkpoint.
/// </summary>
public class CheckpointDto
{
    /// <summary>检查点唯一标识。 / Unique checkpoint identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>检查点名称。 / Checkpoint name.</summary>
    public string? Name { get; set; }

    /// <summary>检查点说明。 / Checkpoint description.</summary>
    public string? Description { get; set; }

    /// <summary>关联的提交 SHA。 / Associated commit SHA.</summary>
    public string? CommitSha { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 获取文件内容的请求。
/// Request used to retrieve file content.
/// </summary>
public class GetFileContentRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>相对项目根目录的文件路径。 / File path relative to the project root.</summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// 获取差异内容的请求。
/// Request used to retrieve diff content.
/// </summary>
public class GetDiffRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>可选的提交 SHA；为空时表示工作区差异。 / Optional commit SHA; when omitted the working tree diff is returned.</summary>
    public string? CommitSha { get; set; }
}
