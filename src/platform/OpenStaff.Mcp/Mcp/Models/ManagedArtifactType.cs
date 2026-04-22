using System.Text.Json.Serialization;

namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示受管安装产物的类型。
/// en: Represents the type of a managed installation artifact.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ManagedArtifactType>))]
public enum ManagedArtifactType
{
    InstallDirectory,
    Manifest,
    SourceMetadata,
    DownloadCache,
    ExtractCache,
    RuntimeBinary,
    PackagePayload
}
