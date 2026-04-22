using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Skills.Services;
/// <summary>
/// zh-CN: 负责按当前上下文解析实际可用的 skill 目录与缺失绑定诊断。
/// en: Resolves usable skill directories and missing-binding diagnostics for the current execution context.
/// </summary>
public sealed class AgentSkillRuntimeService : IAgentSkillRuntimeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IManagedSkillStore _managedSkillStore;
    private readonly ILogger<AgentSkillRuntimeService> _logger;

    public AgentSkillRuntimeService(
        IServiceScopeFactory scopeFactory,
        IManagedSkillStore managedSkillStore,
        ILogger<AgentSkillRuntimeService> logger)
    {
        _scopeFactory = scopeFactory;
        _managedSkillStore = managedSkillStore;
        _logger = logger;
    }

    public async Task<AgentSkillRuntimePayload?> LoadRuntimePayloadAsync(AgentSkillLoadContext context, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var agentRoleSkillBindings = scope.ServiceProvider.GetRequiredService<IAgentRoleSkillBindingRepository>();
        var projectAgentSkillBindings = scope.ServiceProvider.GetRequiredService<IProjectAgentRoleSkillBindingRepository>();
        // Skill 运行时最终认的是“磁盘上真实存在的安装目录”，不是简单认数据库绑定。
        // 所以这里先从 ManagedSkillStore 读取当前机器上真正可用的安装项，再和绑定做匹配。
        var installedByKey = (await _managedSkillStore.GetInstalledAsync(ct))
            .ToDictionary(item => item.InstallKey, StringComparer.OrdinalIgnoreCase);

        var runtimeEntries = new List<AgentSkillRuntimeEntry>();
        var missingBindings = new List<AgentSkillMissingBinding>();

        if (context.Scene == MessageScene.Test && context.AgentRoleId.HasValue)
        {
            // 测试对话走角色级 Skill 绑定。
            var bindings = await agentRoleSkillBindings
                .AsNoTracking()
                .Where(binding => binding.AgentRoleId == context.AgentRoleId.Value && binding.IsEnabled)
                .OrderBy(binding => binding.CreatedAt)
                .ToListAsync(ct);

            ResolveBindings(
                bindings.Select(binding => new RuntimeBindingSnapshot(
                    binding.SkillInstallKey,
                    binding.SkillId,
                    binding.DisplayName,
                    SkillBindingScopes.AgentRoleTest)),
                installedByKey,
                runtimeEntries,
                missingBindings);
        }
        else if (context.ProjectAgentRoleId.HasValue)
        {
            // 正常项目执行走项目成员级 Skill 绑定。
            var bindings = await projectAgentSkillBindings
                .AsNoTracking()
                .Where(binding => binding.ProjectAgentRoleId == context.ProjectAgentRoleId.Value && binding.IsEnabled)
                .OrderBy(binding => binding.CreatedAt)
                .ToListAsync(ct);

            ResolveBindings(
                bindings.Select(binding => new RuntimeBindingSnapshot(
                    binding.SkillInstallKey,
                    binding.SkillId,
                    binding.DisplayName,
                    SkillBindingScopes.ProjectAgentRole)),
                installedByKey,
                runtimeEntries,
                missingBindings);
        }

        if (runtimeEntries.Count == 0 && missingBindings.Count == 0)
            return null;

        if (missingBindings.Count > 0)
        {
            // Skill 缺失也只会记 warning，然后继续执行。
            _logger.LogWarning(
                "Resolved {ResolvedCount} skills with {MissingCount} missing bindings for scene {Scene}",
                runtimeEntries.Count,
                missingBindings.Count,
                context.Scene);
        }

        return new AgentSkillRuntimePayload(runtimeEntries, missingBindings);
    }

    private static void ResolveBindings(
        IEnumerable<RuntimeBindingSnapshot> bindings,
        IReadOnlyDictionary<string, ManagedInstalledSkill> installedByKey,
        ICollection<AgentSkillRuntimeEntry> runtimeEntries,
        ICollection<AgentSkillMissingBinding> missingBindings)
    {
        foreach (var binding in bindings)
        {
            if (!installedByKey.TryGetValue(binding.SkillInstallKey, out var installed))
            {
                // 绑定存在，但磁盘上找不到对应 installKey，就只能记成 missing。
                missingBindings.Add(new AgentSkillMissingBinding(
                    binding.BindingScope,
                    binding.SkillInstallKey,
                    binding.SkillId,
                    binding.DisplayName,
                    "对应的 skill 已不存在，运行时将跳过该绑定。"));
                continue;
            }

            if (!string.Equals(installed.Status, SkillInstallStatuses.Installed, StringComparison.OrdinalIgnoreCase))
            {
                // 找到了安装目录，但状态不是 Installed，依然不能注入给运行时。
                missingBindings.Add(new AgentSkillMissingBinding(
                    binding.BindingScope,
                    binding.SkillInstallKey,
                    binding.SkillId,
                    binding.DisplayName,
                    installed.StatusMessage ?? "对应的 skill 当前不可用，运行时将跳过该绑定。"));
                continue;
            }

            // 这里产出的不是 AITool，而是“技能目录描述”。
            // 下游 Provider 会把这些目录包装成 AIContextProvider 或 SkillDirectories。
            runtimeEntries.Add(new AgentSkillRuntimeEntry(
                installed.InstallKey,
                installed.SkillId,
                installed.DisplayName,
                installed.Source,
                installed.InstallRootPath));
        }
    }

    private sealed record RuntimeBindingSnapshot(
        string SkillInstallKey,
        string SkillId,
        string DisplayName,
        string BindingScope);

    private static class SkillBindingScopes
    {
        public const string AgentRole = "agent-role";
        public const string AgentRoleTest = "agent-role-test";
        public const string ProjectAgentRole = "project-agent";
    }
}

