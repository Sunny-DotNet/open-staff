using System.Text.Json.Serialization;

namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示安装记录的生命周期状态。
/// en: Represents the lifecycle state of an installation record.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstallState>))]
public enum InstallState
{
    Pending,
    Downloading,
    Extracting,
    Installing,
    ResolvingRuntime,
    Ready,
    Failed,
    Uninstalling
}
