using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// Skill 目录与安装管理应用服务。
/// Application service for skill catalog and installation management.
/// </summary>
public interface ISkillApiService : IApiServiceBase
{
    /// <summary>搜索 Skill 目录。 / Searches the skill catalog.</summary>
    Task<SkillCatalogPageDto> SearchCatalogAsync(SkillCatalogQueryInput input, CancellationToken ct = default);

    /// <summary>获取 Skill 来源聚合。 / Gets aggregated skill sources.</summary>
    Task<List<SkillCatalogSourceDto>> GetSourcesAsync(CancellationToken ct = default);

    /// <summary>获取单个 Skill 目录项。 / Gets a single skill catalog item.</summary>
    Task<SkillCatalogItemDto?> GetCatalogItemAsync(string owner, string repo, string skillId, CancellationToken ct = default);

    /// <summary>获取已安装 Skill 列表。 / Gets installed skill records.</summary>
    Task<List<InstalledSkillDto>> GetInstalledAsync(GetInstalledSkillsInput input, CancellationToken ct = default);

    /// <summary>安装 Skill。 / Installs a skill.</summary>
    Task<InstalledSkillDto> InstallAsync(InstallSkillInput input, CancellationToken ct = default);

    /// <summary>卸载 Skill。 / Uninstalls a skill by removing its managed directory.</summary>
    Task<bool> UninstallAsync(UninstallSkillInput input, CancellationToken ct = default);

    /// <summary>获取测试角色 Skill 绑定。 / Gets role-level skill bindings for test chat.</summary>
    Task<List<AgentRoleSkillBindingDto>> GetAgentRoleBindingsAsync(Guid agentRoleId, CancellationToken ct = default);

    /// <summary>替换测试角色 Skill 绑定。 / Replaces role-level skill bindings for test chat.</summary>
    Task ReplaceAgentRoleBindingsAsync(ReplaceAgentRoleSkillBindingsRequest request, CancellationToken ct = default);
}


