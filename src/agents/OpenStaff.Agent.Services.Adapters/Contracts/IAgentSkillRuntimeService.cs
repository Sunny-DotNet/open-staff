using OpenStaff.Agent;
using OpenStaff.Agent.Services;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 为默认应用运行时适配层暴露基于 skill 绑定的原生上下文解析接口。
/// en: Exposes skill-binding based native context resolution for the default application runtime adapters.
/// </summary>
public interface IAgentSkillRuntimeService
{
    /// <summary>
    /// zh-CN: 按当前执行上下文解析可注入的 skills 与缺失绑定诊断。
    /// en: Resolves injectable skills plus missing-binding diagnostics for the current execution context.
    /// </summary>
    Task<AgentSkillRuntimePayload?> LoadRuntimePayloadAsync(AgentSkillLoadContext context, CancellationToken ct);
}

/// <summary>
/// zh-CN: 描述一次消息执行应从哪一层绑定作用域读取 skill。
/// en: Describes which binding scope should be used when loading skills for a message execution.
/// </summary>
public sealed record AgentSkillLoadContext(
    MessageScene Scene,
    Guid? ProjectAgentRoleId,
    Guid? AgentRoleId);
