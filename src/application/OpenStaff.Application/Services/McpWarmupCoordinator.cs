using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenStaff.Mcp;
using OpenStaff.Repositories;

namespace OpenStaff.Application.McpServers.Services;

/// <summary>
/// Coordinates MCP client warmup, promotion to pinned reuse, and rebuilds after configuration changes.
/// </summary>
public sealed class McpWarmupCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpConfigurationFileStore _configurationFileStore;
    private readonly McpResolvedConnectionFactory _resolvedConnectionFactory;
    private readonly McpHub _mcpHub;
    private readonly OpenStaffMcpOptions _options;
    private readonly ILogger<McpWarmupCoordinator> _logger;
    private readonly ConcurrentDictionary<string, ProjectWarmRegistration> _projectRegistrations = new(StringComparer.OrdinalIgnoreCase);

    public McpWarmupCoordinator(
        IServiceScopeFactory scopeFactory,
        IMcpConfigurationFileStore configurationFileStore,
        McpResolvedConnectionFactory resolvedConnectionFactory,
        McpHub mcpHub,
        IOptions<OpenStaffMcpOptions> options,
        ILogger<McpWarmupCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _configurationFileStore = configurationFileStore;
        _resolvedConnectionFactory = resolvedConnectionFactory;
        _mcpHub = mcpHub;
        _options = options.Value;
        _logger = logger;
    }

    public bool EnableStartupWarmup => _options.EnableStartupWarmup;

    public bool PinProjectClientsAfterFirstUse => _options.PinProjectClientsAfterFirstUse;

    public async Task WarmStartupConnectionsAsync(CancellationToken ct = default)
    {
        if (!EnableStartupWarmup)
        {
            _logger.LogInformation("MCP startup warmup is disabled by configuration.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var agentRoleBindings = scope.ServiceProvider.GetRequiredService<IAgentRoleMcpBindingRepository>();
        var roleBindings = await agentRoleBindings
            .AsNoTracking()
            .Include(binding => binding.McpServer)
            .Where(binding => binding.IsEnabled && binding.McpServer != null && binding.McpServer.IsEnabled)
            .OrderBy(binding => binding.CreatedAt)
            .ToListAsync(ct);

        var warmedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        foreach (var binding in roleBindings)
        {
            try
            {
                var configuration = await _configurationFileStore.GetOrCreateGlobalAsync(binding.McpServer!, ct);
                if (!configuration.IsEnabled)
                {
                    skippedCount++;
                    continue;
                }

                var connection = _resolvedConnectionFactory.CreateForAgentRole(
                    binding.McpServer!,
                    binding.AgentRoleId,
                    configuration);
                var skipReason = await _mcpHub.GetPreloadSkipReasonAsync(connection, ct);
                if (!string.IsNullOrWhiteSpace(skipReason))
                {
                    skippedCount++;
                    _logger.LogInformation(
                        "Skipped MCP startup warmup for agent role {AgentRoleId} on server {ServerId}: {Reason}",
                        binding.AgentRoleId,
                        binding.McpServerId,
                        skipReason);
                    continue;
                }

                await _mcpHub.WarmAsync(
                    connection,
                    warmReason: "startup-global",
                    pinClient: true,
                    preloadToolSnapshot: true,
                    ct);
                warmedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to warm global MCP client for agent role {AgentRoleId} on server {ServerId}",
                    binding.AgentRoleId,
                    binding.McpServerId);
            }
        }

        _logger.LogInformation(
            "Completed MCP startup warmup for global bindings: {WarmedCount} warmed, {SkippedCount} skipped, {FailedCount} failed.",
            warmedCount,
            skippedCount,
            failedCount);
    }

    public async Task PromoteProjectConnectionAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken ct = default)
    {
        if (!PinProjectClientsAfterFirstUse
            || !connection.ProjectId.HasValue
            || !connection.AgentRoleId.HasValue)
        {
            return;
        }

        _projectRegistrations[connection.CacheKey] = new ProjectWarmRegistration(
            connection.CacheKey,
            connection.ProjectId.Value,
            connection.AgentRoleId.Value,
            connection.ServerId);

        await _mcpHub.WarmAsync(
            connection,
            warmReason: "project-first-use",
            pinClient: true,
            preloadToolSnapshot: false,
            ct);
    }

    public Task RebuildServerAsync(Guid serverId, CancellationToken ct = default)
        => RebuildAsync(
            globalFilter: binding => binding.McpServerId == serverId,
            projectFilter: registration => registration.ServerId == serverId,
            reason: $"server:{serverId:N}",
            ct);

    public Task RebuildAgentRoleAsync(Guid agentRoleId, CancellationToken ct = default)
        => RebuildAsync(
            globalFilter: binding => binding.AgentRoleId == agentRoleId,
            projectFilter: registration => registration.AgentRoleId == agentRoleId,
            reason: $"agent-role:{agentRoleId:N}",
            ct);

    public Task RebuildProjectAsync(Guid projectId, CancellationToken ct = default)
        => RebuildAsync(
            globalFilter: _ => false,
            projectFilter: registration => registration.ProjectId == projectId,
            reason: $"project:{projectId:N}",
            ct);

    public void ForgetProject(Guid projectId)
    {
        var removedCount = RemoveRegistrations(registration => registration.ProjectId == projectId);
        if (removedCount > 0)
        {
            _logger.LogInformation("Forgot {Count} project MCP warm registrations for project {ProjectId}", removedCount, projectId);
        }
    }

    public void ForgetServer(Guid serverId)
    {
        var removedCount = RemoveRegistrations(registration => registration.ServerId == serverId);
        if (removedCount > 0)
        {
            _logger.LogInformation("Forgot {Count} MCP warm registrations for server {ServerId}", removedCount, serverId);
        }
    }

    private async Task RebuildAsync(
        Func<AgentRoleMcpBinding, bool> globalFilter,
        Func<ProjectWarmRegistration, bool> projectFilter,
        string reason,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var agentRoleBindings = scope.ServiceProvider.GetRequiredService<IAgentRoleMcpBindingRepository>();
        var projectRepository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

        var globalBindings = await agentRoleBindings
            .AsNoTracking()
            .Include(binding => binding.McpServer)
            .Where(binding => binding.IsEnabled && binding.McpServer != null && binding.McpServer.IsEnabled)
            .ToListAsync(ct);

        var filteredGlobalBindings = globalBindings.Where(globalFilter).ToList();
        var projectRegistrations = _projectRegistrations.Values
            .Where(projectFilter)
            .ToList();

        var warmedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        foreach (var binding in filteredGlobalBindings)
        {
            try
            {
                var configuration = await _configurationFileStore.GetOrCreateGlobalAsync(binding.McpServer!, ct);
                if (!configuration.IsEnabled)
                {
                    skippedCount++;
                    continue;
                }

                var connection = _resolvedConnectionFactory.CreateForAgentRole(
                    binding.McpServer!,
                    binding.AgentRoleId,
                    configuration);
                var skipReason = await _mcpHub.GetPreloadSkipReasonAsync(connection, ct);
                if (!string.IsNullOrWhiteSpace(skipReason))
                {
                    skippedCount++;
                    _logger.LogInformation(
                        "Skipped MCP global rebuild for reason {Reason}, role {AgentRoleId}, server {ServerId}: {SkipReason}",
                        reason,
                        binding.AgentRoleId,
                        binding.McpServerId,
                        skipReason);
                    continue;
                }

                await _mcpHub.WarmAsync(
                    connection,
                    warmReason: $"rebuild-global:{reason}",
                    pinClient: true,
                    preloadToolSnapshot: true,
                    ct);
                warmedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to rebuild global MCP warm client for reason {Reason}, role {AgentRoleId}, server {ServerId}",
                    reason,
                    binding.AgentRoleId,
                    binding.McpServerId);
            }
        }

        foreach (var registration in projectRegistrations)
        {
            try
            {
                var project = await projectRepository
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == registration.ProjectId, ct);
                if (project == null)
                {
                    _projectRegistrations.TryRemove(registration.CacheKey, out _);
                    skippedCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(project.WorkspacePath))
                {
                    skippedCount++;
                    continue;
                }

                var binding = globalBindings.FirstOrDefault(item =>
                    item.AgentRoleId == registration.AgentRoleId
                    && item.McpServerId == registration.ServerId);
                if (binding?.McpServer == null)
                {
                    _projectRegistrations.TryRemove(registration.CacheKey, out _);
                    skippedCount++;
                    continue;
                }

                var configuration = await _configurationFileStore.GetProjectOverrideAsync(
                        binding.McpServerId,
                        project.WorkspacePath,
                        ct)
                    ?? await _configurationFileStore.GetOrCreateGlobalAsync(binding.McpServer, ct);
                if (!configuration.IsEnabled)
                {
                    skippedCount++;
                    continue;
                }

                var connection = _resolvedConnectionFactory.CreateForProject(
                    binding.McpServer,
                    registration.ProjectId,
                    registration.AgentRoleId,
                    project.WorkspacePath,
                    configuration);
                var skipReason = await _mcpHub.GetPreloadSkipReasonAsync(connection, ct);
                if (!string.IsNullOrWhiteSpace(skipReason))
                {
                    skippedCount++;
                    _logger.LogInformation(
                        "Skipped MCP project rebuild for reason {Reason}, project {ProjectId}, role {AgentRoleId}, server {ServerId}: {SkipReason}",
                        reason,
                        registration.ProjectId,
                        registration.AgentRoleId,
                        registration.ServerId,
                        skipReason);
                    continue;
                }

                await _mcpHub.WarmAsync(
                    connection,
                    warmReason: $"rebuild-project:{reason}",
                    pinClient: true,
                    preloadToolSnapshot: true,
                    ct);
                warmedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to rebuild project MCP warm client for reason {Reason}, project {ProjectId}, role {AgentRoleId}, server {ServerId}",
                    reason,
                    registration.ProjectId,
                    registration.AgentRoleId,
                    registration.ServerId);
            }
        }

        if (filteredGlobalBindings.Count > 0 || projectRegistrations.Count > 0)
        {
            _logger.LogInformation(
                "Rebuilt MCP warm cache for {Reason}: {WarmedCount} warmed, {SkippedCount} skipped, {FailedCount} failed.",
                reason,
                warmedCount,
                skippedCount,
                failedCount);
        }
    }

    private int RemoveRegistrations(Func<ProjectWarmRegistration, bool> predicate)
    {
        var removedCount = 0;
        foreach (var registration in _projectRegistrations.Values.Where(predicate).ToList())
        {
            if (_projectRegistrations.TryRemove(registration.CacheKey, out _))
            {
                removedCount++;
            }
        }

        return removedCount;
    }

    private sealed record ProjectWarmRegistration(
        string CacheKey,
        Guid ProjectId,
        Guid AgentRoleId,
        Guid ServerId);
}
