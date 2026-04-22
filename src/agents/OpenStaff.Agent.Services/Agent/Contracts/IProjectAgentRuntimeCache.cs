namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 在项目级智能体能力或配置变更时失效相关运行时缓存。
/// en: Invalidates cached runtime instances when project-level agent capabilities or configuration change.
/// </summary>
public interface IProjectAgentRuntimeCache
{
    /// <summary>
    /// zh-CN: 失效指定项目中某个角色类型的运行时缓存。
    /// en: Invalidates the runtime cache for a role type within the specified project.
    /// </summary>
    void InvalidateProjectAgent(Guid projectId, string roleType);
}
