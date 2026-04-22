using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Projects.Services;

/// <summary>
/// zh-CN: 负责为内置职责补默认 MCP/Skill，并把角色级默认绑定复制到项目成员。
/// en: Seeds default MCP/skill bindings for known roles and copies role defaults into project-scoped agents.
/// </summary>
public sealed class RoleCapabilityBindingService
{
    private readonly IAgentRoleRepository _agentRoles;
    private readonly IProjectAgentRoleRepository _projectAgents;
    private readonly IMcpServerRepository _mcpServers;
    private readonly IAgentRoleMcpBindingRepository _agentRoleMcpBindings;
    private readonly IAgentRoleSkillBindingRepository _agentRoleSkillBindings;
    private readonly IProjectAgentRoleSkillBindingRepository _projectAgentSkillBindings;
    private readonly IManagedSkillStore _managedSkillStore;
    private readonly IRepositoryContext _repositoryContext;
    private readonly ILogger<RoleCapabilityBindingService> _logger;

    public RoleCapabilityBindingService(
        IAgentRoleRepository agentRoles,
        IProjectAgentRoleRepository projectAgents,
        IMcpServerRepository mcpServers,
        IAgentRoleMcpBindingRepository agentRoleMcpBindings,
        IAgentRoleSkillBindingRepository agentRoleSkillBindings,
        IProjectAgentRoleSkillBindingRepository projectAgentSkillBindings,
        IManagedSkillStore managedSkillStore,
        IRepositoryContext repositoryContext,
        ILogger<RoleCapabilityBindingService> logger)
    {
        _agentRoles = agentRoles;
        _projectAgents = projectAgents;
        _mcpServers = mcpServers;
        _agentRoleMcpBindings = agentRoleMcpBindings;
        _agentRoleSkillBindings = agentRoleSkillBindings;
        _projectAgentSkillBindings = projectAgentSkillBindings;
        _managedSkillStore = managedSkillStore;
        _repositoryContext = repositoryContext;
        _logger = logger;
    }

    /// <summary>
    /// zh-CN: 为未配置过默认工具链的角色补齐推荐 MCP/Skill。
    /// en: Seeds recommended MCP/skill bindings for matched roles that do not have any bindings yet.
    /// </summary>
    public async Task<int> SeedDefaultRoleBindingsAsync(CancellationToken ct = default)
    {
        var roles = await _agentRoles.AsNoTracking()
            .Where(role => role.IsActive)
            .ToListAsync(ct);

        var matchedRoles = roles
            .Select(role => (Role: role, Profile: ResolveProfile(role)))
            .Where(item => item.Profile is not null)
            .Select(item => (item.Role, Profile: item.Profile!))
            .ToList();
        if (matchedRoles.Count == 0)
            return 0;

        var roleIds = matchedRoles.Select(item => item.Role.Id).ToList();
        var existingRoleMcpBindings = await _agentRoleMcpBindings.AsNoTracking()
            .Where(binding => roleIds.Contains(binding.AgentRoleId))
            .Select(binding => new { binding.AgentRoleId, binding.McpServerId })
            .ToListAsync(ct);
        var existingRoleSkillBindings = await _agentRoleSkillBindings.AsNoTracking()
            .Where(binding => roleIds.Contains(binding.AgentRoleId))
            .Select(binding => new { binding.AgentRoleId, binding.SkillInstallKey })
            .ToListAsync(ct);

        var existingRoleMcpServerIdsByRoleId = existingRoleMcpBindings
            .GroupBy(binding => binding.AgentRoleId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(binding => binding.McpServerId).ToHashSet());
        var existingRoleSkillKeysByRoleId = existingRoleSkillBindings
            .GroupBy(binding => binding.AgentRoleId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(binding => binding.SkillInstallKey).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var serversByName = (await _mcpServers.AsNoTracking()
                .Where(server => server.IsEnabled)
                .ToListAsync(ct))
            .GroupBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var installedSkillsByIdentity = (await _managedSkillStore.GetInstalledAsync(ct))
            .Where(skill => string.Equals(skill.Status, SkillInstallStatuses.Installed, StringComparison.OrdinalIgnoreCase))
            .GroupBy(skill => BuildSkillIdentity(skill.Owner, skill.Repo, skill.SkillId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var addedBindings = 0;
        foreach (var (role, profile) in matchedRoles)
        {
            var existingMcpServerIds = existingRoleMcpServerIdsByRoleId.TryGetValue(role.Id, out var roleMcpServerIds)
                ? roleMcpServerIds
                : [];
            foreach (var serverName in profile.McpServers)
            {
                if (!serversByName.TryGetValue(serverName, out var server) || existingMcpServerIds.Contains(server.Id))
                    continue;

                _agentRoleMcpBindings.Add(new AgentRoleMcpBinding
                {
                    AgentRoleId = role.Id,
                    McpServerId = server.Id,
                    ToolFilter = null,
                    IsEnabled = true
                });
                existingMcpServerIds.Add(server.Id);
                addedBindings++;
            }

            var existingSkillKeys = existingRoleSkillKeysByRoleId.TryGetValue(role.Id, out var roleSkillKeys)
                ? roleSkillKeys
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var skillRef in profile.Skills)
            {
                if (!installedSkillsByIdentity.TryGetValue(skillRef.Identity, out var installedSkill)
                    || existingSkillKeys.Contains(installedSkill.InstallKey))
                {
                    continue;
                }

                _agentRoleSkillBindings.Add(CreateRoleSkillBinding(role.Id, installedSkill));
                existingSkillKeys.Add(installedSkill.InstallKey);
                addedBindings++;
            }
        }

        if (addedBindings > 0)
        {
            await _repositoryContext.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {BindingCount} default role capability bindings", addedBindings);
        }

        return addedBindings;
    }

    /// <summary>
    /// zh-CN: 为历史项目里仍为空的成员补齐角色级默认 Skill 绑定。
    /// en: Backfills missing project-agent skill bindings from role defaults for existing project memberships.
    /// </summary>
    public async Task<int> SeedMissingProjectAgentBindingsAsync(CancellationToken ct = default)
    {
        var projectAgents = await _projectAgents.AsNoTracking()
            .ToListAsync(ct);
        if (projectAgents.Count == 0)
            return 0;

        var projectAgentIds = projectAgents.Select(agent => agent.Id).ToList();
        var existingProjectSkillBindings = await _projectAgentSkillBindings.AsNoTracking()
            .Where(binding => projectAgentIds.Contains(binding.ProjectAgentRoleId))
            .Select(binding => new { binding.ProjectAgentRoleId, binding.SkillInstallKey })
            .ToListAsync(ct);

        var existingProjectSkillKeysByAgentId = existingProjectSkillBindings
            .GroupBy(binding => binding.ProjectAgentRoleId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(binding => binding.SkillInstallKey).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var requests = projectAgents
            .Select(agent => new ProjectAgentBindingProvisionRequest(
                agent,
                ExistingSkillInstallKeys: existingProjectSkillKeysByAgentId.TryGetValue(agent.Id, out var skillInstallKeys)
                    ? skillInstallKeys
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            .ToList();

        return await ProvisionProjectAgentBindingsAsync(requests, ct);
    }

    /// <summary>
    /// zh-CN: 将角色级默认 Skill 绑定复制到刚创建的项目成员。
    /// en: Copies role-level skill bindings into newly created project-scoped agents.
    /// </summary>
    public Task<int> CopyRoleBindingsToProjectAgentsAsync(
        IReadOnlyCollection<ProjectAgentRole> projectAgents,
        CancellationToken ct = default,
        bool saveChanges = true)
    {
        ArgumentNullException.ThrowIfNull(projectAgents);

        var requests = projectAgents
            .Where(agent => agent.AgentRoleId != Guid.Empty && agent.ProjectId != Guid.Empty)
            .Select(agent => new ProjectAgentBindingProvisionRequest(
                agent,
                ExistingSkillInstallKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            .ToList();

        return ProvisionProjectAgentBindingsAsync(requests, ct, saveChanges);
    }

    private async Task<int> ProvisionProjectAgentBindingsAsync(
        IReadOnlyCollection<ProjectAgentBindingProvisionRequest> requests,
        CancellationToken ct,
        bool saveChanges = true)
    {
        if (requests.Count == 0)
            return 0;

        var roleIds = requests
            .Select(request => request.Agent.AgentRoleId)
            .Distinct()
            .ToList();

        var roleSkillBindingsByRoleId = (await _agentRoleSkillBindings.AsNoTracking()
                .Where(binding => roleIds.Contains(binding.AgentRoleId))
                .OrderBy(binding => binding.CreatedAt)
                .ToListAsync(ct))
            .GroupBy(binding => binding.AgentRoleId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var addedBindings = 0;
        foreach (var request in requests)
        {
            if (roleSkillBindingsByRoleId.TryGetValue(request.Agent.AgentRoleId, out var roleSkillBindings))
            {
                foreach (var roleBinding in roleSkillBindings)
                {
                    if (request.ExistingSkillInstallKeys.Contains(roleBinding.SkillInstallKey))
                        continue;

                    _projectAgentSkillBindings.Add(new ProjectAgentRoleSkillBinding
                    {
                        ProjectAgentRoleId = request.Agent.Id,
                        SkillInstallKey = roleBinding.SkillInstallKey,
                        SkillId = roleBinding.SkillId,
                        Name = roleBinding.Name,
                        DisplayName = roleBinding.DisplayName,
                        Source = roleBinding.Source,
                        Owner = roleBinding.Owner,
                        Repo = roleBinding.Repo,
                        GithubUrl = roleBinding.GithubUrl,
                        IsEnabled = roleBinding.IsEnabled
                    });
                    request.ExistingSkillInstallKeys.Add(roleBinding.SkillInstallKey);
                    addedBindings++;
                }
            }
        }

        if (saveChanges && addedBindings > 0)
            await _repositoryContext.SaveChangesAsync(ct);

        return addedBindings;
    }

    private static AgentRoleSkillBinding CreateRoleSkillBinding(Guid roleId, ManagedInstalledSkill installedSkill)
        => new()
        {
            AgentRoleId = roleId,
            SkillInstallKey = installedSkill.InstallKey,
            SkillId = installedSkill.SkillId,
            Name = installedSkill.Name,
            DisplayName = installedSkill.DisplayName,
            Source = string.IsNullOrWhiteSpace(installedSkill.Source)
                ? $"{installedSkill.Owner}/{installedSkill.Repo}"
                : installedSkill.Source,
            Owner = installedSkill.Owner,
            Repo = installedSkill.Repo,
            GithubUrl = installedSkill.GithubUrl,
            IsEnabled = true
        };

    private static string BuildSkillIdentity(string owner, string repo, string skillId)
        => $"{owner.Trim()}/{repo.Trim()}:{skillId.Trim()}";

    private static RoleCapabilityProfile? ResolveProfile(AgentRole role)
    {
        foreach (var profile in Profiles)
        {
            if (profile.Matches(role))
                return profile;
        }

        return null;
    }

    private static readonly IReadOnlyList<RoleCapabilityProfile> Profiles =
    [
        new(
            NameMatches: ["Monica"],
            JobTitleMatches: ["秘书", "secretary"],
            McpServers:
            [
                "Filesystem",
                "Everything",
                "Fetch",
                "Brave Search",
                "GitHub",
                "Memory",
                "Sequential Thinking",
                "docs-mcp",
                "Agent Skills Search Server"
            ],
            Skills:
            [
                new SkillCapabilityReference("github", "awesome-copilot", "gh-cli"),
                new SkillCapabilityReference("anthropics", "skills", "docx"),
                new SkillCapabilityReference("anthropics", "skills", "xlsx"),
                new SkillCapabilityReference("anthropics", "skills", "pdf"),
                new SkillCapabilityReference("anthropics", "skills", "pptx"),
                new SkillCapabilityReference("vercel-labs", "skills", "find-skills"),
                new SkillCapabilityReference("vercel-labs", "agent-browser", "agent-browser"),
                new SkillCapabilityReference("obra", "superpowers", "brainstorming")
            ]),
        new(
            NameMatches: ["Sophie"],
            JobTitleMatches: ["后端工程师", "backend engineer"],
            McpServers:
            [
                "Filesystem",
                "Everything",
                "GitHub",
                "Fetch",
                "PostgreSQL",
                "SQLite",
                "OpenStaff Shell",
                "Sequential Thinking",
                "docs-mcp"
            ],
            Skills:
            [
                new SkillCapabilityReference("github", "awesome-copilot", "gh-cli"),
                new SkillCapabilityReference("supabase", "agent-skills", "supabase-postgres-best-practices"),
                new SkillCapabilityReference("wshobson", "agents", "postgresql-table-design")
            ]),
        new(
            NameMatches: ["Jennifer"],
            JobTitleMatches: ["软件工程师", "software engineer", "developer"],
            McpServers:
            [
                "Filesystem",
                "Everything",
                "GitHub",
                "Fetch",
                "PostgreSQL",
                "SQLite",
                "OpenStaff Shell",
                "Sequential Thinking",
                "docs-mcp"
            ],
            Skills:
            [
                new SkillCapabilityReference("github", "awesome-copilot", "gh-cli"),
                new SkillCapabilityReference("github", "awesome-copilot", "git-commit"),
                new SkillCapabilityReference("sickn33", "antigravity-awesome-skills", "address-github-comments")
            ]),
        new(
            NameMatches: ["圆圆"],
            JobTitleMatches: ["代码审查员", "code reviewer"],
            McpServers:
            [
                "Filesystem",
                "Everything",
                "GitHub",
                "Fetch",
                "OpenStaff Shell",
                "Sequential Thinking",
                "docs-mcp"
            ],
            Skills:
            [
                new SkillCapabilityReference("github", "awesome-copilot", "gh-cli"),
                new SkillCapabilityReference("anthropics", "skills", "pdf"),
                new SkillCapabilityReference("wshobson", "agents", "code-review-excellence")
            ]),
        new(
            NameMatches: ["菲菲"],
            JobTitleMatches: ["美工", "designer", "ui designer"],
            McpServers:
            [
                "Filesystem",
                "Everything",
                "Fetch",
                "Brave Search",
                "Puppeteer"
            ],
            Skills:
            [
                new SkillCapabilityReference("anthropics", "skills", "frontend-design"),
                new SkillCapabilityReference("anthropics", "skills", "pptx"),
                new SkillCapabilityReference("anthropics", "skills", "pdf"),
                new SkillCapabilityReference("vercel-labs", "agent-skills", "web-design-guidelines"),
                new SkillCapabilityReference("anthropics", "skills", "canvas-design")
            ])
    ];

    private sealed record RoleCapabilityProfile(
        IReadOnlyList<string> NameMatches,
        IReadOnlyList<string> JobTitleMatches,
        IReadOnlyList<string> McpServers,
        IReadOnlyList<SkillCapabilityReference> Skills)
    {
        public bool Matches(AgentRole role)
        {
            return MatchesAny(NameMatches, role.Name)
                || MatchesAny(JobTitleMatches, role.JobTitle);
        }

        private static bool MatchesAny(IReadOnlyList<string> expectedValues, string? actualValue)
        {
            if (string.IsNullOrWhiteSpace(actualValue))
                return false;

            return expectedValues.Any(expected =>
                string.Equals(expected, actualValue.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed record SkillCapabilityReference(string Owner, string Repo, string SkillId)
    {
        public string Identity => BuildSkillIdentity(Owner, Repo, SkillId);
    }

    private sealed record ProjectAgentBindingProvisionRequest(
        ProjectAgentRole Agent,
        HashSet<string> ExistingSkillInstallKeys);
}
