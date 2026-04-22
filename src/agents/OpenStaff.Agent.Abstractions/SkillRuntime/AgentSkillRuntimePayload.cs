using OpenStaff.Core.Agents;

namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 描述运行时中一个已解析的 skill 目录。
/// en: Describes a resolved skill directory used at runtime.
/// </summary>
public sealed record AgentSkillRuntimeEntry(
    string InstallKey,
    string SkillId,
    string DisplayName,
    string Source,
    string DirectoryPath);

/// <summary>
/// zh-CN: 描述一个在运行时未能解析到真实 skill 目录的绑定。
/// en: Describes a binding that could not be resolved to a real skill directory at runtime.
/// </summary>
public sealed record AgentSkillMissingBinding(
    string BindingScope,
    string SkillInstallKey,
    string SkillId,
    string DisplayName,
    string Message);

/// <summary>
/// zh-CN: Skill 运行时解析结果，包含可注入目录和缺失诊断。
/// en: Skill runtime resolution result containing injectable directories and missing-binding diagnostics.
/// </summary>
public sealed record AgentSkillRuntimePayload(
    IReadOnlyList<AgentSkillRuntimeEntry> Skills,
    IReadOnlyList<AgentSkillMissingBinding> MissingBindings)
{
    /// <summary>
    /// zh-CN: 是否存在至少一个可用的 skill 目录。
    /// en: Indicates whether at least one usable skill directory is available.
    /// </summary>
    public bool HasResolvedSkills => Skills.Count > 0;
}

/// <summary>
/// zh-CN: 为 <see cref="AgentContext.ExtraConfig" /> 提供 skill runtime payload 的读写扩展。
/// en: Provides helpers for storing and reading the skill runtime payload from <see cref="AgentContext.ExtraConfig" />.
/// </summary>
public static class AgentSkillRuntimeContextExtensions
{
    private const string SkillRuntimePayloadKey = "skills.runtime.payload";

    /// <summary>
    /// zh-CN: 将 skill runtime payload 挂到当前上下文。
    /// en: Attaches the skill runtime payload to the current agent context.
    /// </summary>
    public static void SetSkillRuntimePayload(this AgentContext context, AgentSkillRuntimePayload payload)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(payload);
        context.ExtraConfig[SkillRuntimePayloadKey] = payload;
    }

    /// <summary>
    /// zh-CN: 读取当前上下文中的 skill runtime payload。
    /// en: Reads the skill runtime payload attached to the current agent context.
    /// </summary>
    public static AgentSkillRuntimePayload? GetSkillRuntimePayload(this AgentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.ExtraConfig.TryGetValue(SkillRuntimePayloadKey, out var payload)
            ? payload as AgentSkillRuntimePayload
            : null;
    }

    /// <summary>
    /// zh-CN: 读取当前上下文中已解析 skill 的实际目录列表，并去重空值。
    /// en: Reads the resolved runtime skill directories from the current context and removes empty/duplicate values.
    /// </summary>
    public static IReadOnlyList<string> GetResolvedSkillDirectories(this AgentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var payload = context.GetSkillRuntimePayload();
        if (payload?.Skills is not { Count: > 0 })
            return [];

        return payload.Skills
            .Select(skill => skill.DirectoryPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
