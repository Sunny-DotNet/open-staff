using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Entities;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Services;
using OpenStaff.Repositories;

namespace OpenStaff.Application.McpServers.Services;

/// <summary>
/// Bridges OpenStaff.Mcp uninstall checks with application-layer role bindings and project override files.
/// </summary>
public sealed class ApplicationMcpReferenceInspector : IMcpReferenceInspector
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ApplicationMcpReferenceInspector(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<McpReferenceInspectionResult> InspectAsync(
        InstalledMcp installedMcp,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        var mcpServers = scope.ServiceProvider.GetRequiredService<IMcpServerRepository>();
        var projectAgents = scope.ServiceProvider.GetRequiredService<IProjectAgentRoleRepository>();
        var roleBindings = scope.ServiceProvider.GetRequiredService<IAgentRoleMcpBindingRepository>();
        var configurationFileStore = scope.ServiceProvider.GetRequiredService<IMcpConfigurationFileStore>();

        var candidateServers = await mcpServers.AsNoTracking()
            .Where(server => server.InstallInfo != null)
            .Select(server => new { server.Id, server.Name, server.InstallInfo })
            .ToListAsync(cancellationToken);

        var matchingServers = candidateServers
            .Where(server => McpManagedInstallInfo.TryParse(server.InstallInfo)?.InstallId == installedMcp.InstallId)
            .ToList();

        if (matchingServers.Count == 0)
            return new McpReferenceInspectionResult();

        var serverIds = matchingServers.Select(server => server.Id).ToList();
        var boundRoles = await roleBindings.AsNoTracking()
            .Where(binding => serverIds.Contains(binding.McpServerId))
            .Include(binding => binding.AgentRole)
            .Select(binding => binding.AgentRole!.Name)
            .ToListAsync(cancellationToken);

        var projectOverrideReferences = new List<string>();
        var agents = await projectAgents.AsNoTracking()
            .Include(agent => agent.Project)
            .Include(agent => agent.AgentRole)
            .Where(agent => agent.Project != null && agent.AgentRole != null)
            .ToListAsync(cancellationToken);
        foreach (var serverId in serverIds)
        {
            foreach (var agent in agents)
            {
                var projectOverride = await configurationFileStore.GetProjectOverrideAsync(
                    serverId,
                    agent.Project?.WorkspacePath,
                    cancellationToken);
                if (projectOverride?.Exists != true)
                    continue;

                projectOverrideReferences.Add($"{agent.Project!.Name} / {agent.AgentRole!.Name}");
            }
        }

        return new McpReferenceInspectionResult
        {
            BlockingReasons = BuildBlockingReasons(projectOverrideReferences.Count, boundRoles.Count),
            ReferencedByConfigs = [],
            ReferencedByProjectBindings = projectOverrideReferences
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ReferencedByRoleBindings = boundRoles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static List<string> BuildBlockingReasons(int projectOverrideCount, int roleBindingCount)
    {
        var reasons = new List<string>();
        if (projectOverrideCount > 0)
            reasons.Add("Project MCP overrides still reference this install.");
        if (roleBindingCount > 0)
            reasons.Add("Agent-role bindings still reference this install.");
        return reasons;
    }
}
