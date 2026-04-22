using OpenStaff.Agent.Services;
using OpenStaff.Entities;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 统一解析和规范化运行时投影元数据，方便应用层读取持久化状态。
/// en: Normalizes and parses runtime projection metadata so application services can read persisted runtime state consistently.
/// </summary>
public static class RuntimeProjectionMetadataMapper
{
    /// <summary>
    /// zh-CN: 解析智能体事件元数据负载。
    /// en: Parses the agent-event metadata payload.
    /// </summary>
    public static AgentEventMetadataPayload? ParseAgentEventMetadata(string? metadata)
        => AgentEventMetadataPayload.TryParse(metadata);

    /// <summary>
    /// zh-CN: 解析任务运行时元数据负载。
    /// en: Parses the task runtime metadata payload.
    /// </summary>
    public static TaskItemRuntimeMetadata? ParseTaskMetadata(string? metadata)
        => TaskItemRuntimeMetadata.TryParse(metadata);

    /// <summary>
    /// zh-CN: 将枚举场景规范化为持久化字符串。
    /// en: Normalizes a scene enum into its persisted string representation.
    /// </summary>
    public static string NormalizeScene(MessageScene scene)
        => scene.ToString();

    /// <summary>
    /// zh-CN: 将任意场景字符串规范化为运行时使用的标准大小写。
    /// en: Normalizes an arbitrary scene string to the canonical runtime casing.
    /// </summary>
    public static string? NormalizeScene(string? scene)
    {
        if (string.IsNullOrWhiteSpace(scene))
            return null;

        return Enum.TryParse<MessageScene>(scene, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : scene.Trim();
    }
}
