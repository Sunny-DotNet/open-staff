using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Orchestration.Services;
/// <summary>
/// 智能体 MCP 工具桥接服务，负责加载和放宽角色可用工具集合。
/// MCP tool bridge service that loads and relaxes the tool set available to an agent role.
/// </summary>
public sealed class AgentMcpToolService : IAgentMcpToolService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpConfigurationFileStore _configurationFileStore;
    private readonly McpResolvedConnectionFactory _resolvedConnectionFactory;
    private readonly McpHub _mcpHub;
    private readonly McpWarmupCoordinator? _mcpWarmupCoordinator;
    private readonly ILogger<AgentMcpToolService> _logger;

    /// <summary>
    /// 初始化智能体 MCP 工具桥接服务。
    /// Initializes the agent MCP tool bridge service.
    /// </summary>
    public AgentMcpToolService(
        IServiceScopeFactory scopeFactory,
        IMcpConfigurationFileStore configurationFileStore,
        McpResolvedConnectionFactory resolvedConnectionFactory,
        McpHub mcpHub,
        ILogger<AgentMcpToolService> logger,
        McpWarmupCoordinator? mcpWarmupCoordinator = null)
    {
        _scopeFactory = scopeFactory;
        _configurationFileStore = configurationFileStore;
        _resolvedConnectionFactory = resolvedConnectionFactory;
        _mcpHub = mcpHub;
        _mcpWarmupCoordinator = mcpWarmupCoordinator;
        _logger = logger;
    }

    /// <summary>
    /// 按执行上下文加载已启用的 MCP 工具。
    /// Loads the enabled MCP tools for the current execution context.
    /// </summary>
    public async Task<List<AITool>> LoadEnabledToolsAsync(AgentMcpToolLoadContext context, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var agentRoleMcpBindings = scope.ServiceProvider.GetRequiredService<IAgentRoleMcpBindingRepository>();
        var projectAgents = scope.ServiceProvider.GetRequiredService<IProjectAgentRoleRepository>();

        var tools = new List<AITool>();
        if (context.Scene == MessageScene.Test && context.AgentRoleId.HasValue)
        {
            var bindings = await agentRoleMcpBindings
                .AsNoTracking()
                .Include(binding => binding.McpServer)
                .Where(binding => binding.AgentRoleId == context.AgentRoleId.Value && binding.IsEnabled && binding.McpServer!.IsEnabled)
                .ToListAsync(ct);

            foreach (var binding in bindings)
            {
                try
                {
                    var configuration = await _configurationFileStore.GetOrCreateGlobalAsync(binding.McpServer!, ct);
                    if (!configuration.IsEnabled)
                        continue;

                    var connection = _resolvedConnectionFactory.CreateForAgentRole(
                        binding.McpServer!,
                        context.AgentRoleId.Value,
                        configuration,
                        sessionId: context.SessionId,
                        scene: context.Scene.ToString(),
                        dispatchSource: context.DispatchSource);
                    var serverTools = await _mcpHub.GetToolsAsync(connection, ct);
                    await _mcpHub.WarmAsync(
                        connection,
                        warmReason: "global-use",
                        pinClient: true,
                        preloadToolSnapshot: false,
                        ct);
                    if (!string.IsNullOrWhiteSpace(binding.ToolFilter))
                    {
                        var filter = ParseToolFilter(binding.ToolFilter);
                        if (filter.Count > 0)
                        {
                            serverTools = serverTools
                                .Where(tool => filter.Contains(tool.Name))
                                .ToList();
                        }
                    }

                    tools.AddRange(serverTools.Select(tool => tool.Tool));
                }
                catch (Exception ex)
                {
                    // 这里故意降级为 warning：单个 MCP 挂掉不应该拖垮整个对话。
                    // 但副作用就是如果没有把日志抛到前端，看起来就像“工具没创建成功”。
                    _logger.LogWarning(
                        ex,
                        "Failed to load MCP tools for agent role {AgentRoleId} from server {ServerId}",
                        context.AgentRoleId,
                        binding.McpServerId);
                }
            }

            return tools;
        }

        if (!context.ProjectAgentRoleId.HasValue)
            return [];

        var projectAgent = await projectAgents
            .AsNoTracking()
            .Include(agent => agent.Project)
            .FirstOrDefaultAsync(agent => agent.Id == context.ProjectAgentRoleId.Value, ct);
        if (projectAgent == null)
            return [];

        var roleBindings = await agentRoleMcpBindings
            .AsNoTracking()
            .Include(binding => binding.McpServer)
            .Where(binding => binding.AgentRoleId == projectAgent.AgentRoleId && binding.IsEnabled && binding.McpServer!.IsEnabled)
            .ToListAsync(ct);

        foreach (var binding in roleBindings)
        {
            try
            {
                var configuration = await _configurationFileStore.GetProjectOverrideAsync(
                        binding.McpServerId,
                        projectAgent.Project?.WorkspacePath,
                        ct)
                    ?? await _configurationFileStore.GetOrCreateGlobalAsync(binding.McpServer!, ct);
                if (!configuration.IsEnabled)
                    continue;

                var connection = _resolvedConnectionFactory.CreateForProject(
                    binding.McpServer!,
                    projectAgent.ProjectId,
                    projectAgent.AgentRoleId,
                    projectAgent.Project?.WorkspacePath,
                    configuration,
                    sessionId: context.SessionId,
                    scene: context.Scene.ToString(),
                    projectAgentRoleId: context.ProjectAgentRoleId,
                    dispatchSource: context.DispatchSource);
                var serverTools = await _mcpHub.GetToolsAsync(connection, ct);
                if (_mcpWarmupCoordinator != null)
                {
                    await _mcpWarmupCoordinator.PromoteProjectConnectionAsync(connection, ct);
                }
                if (!string.IsNullOrWhiteSpace(binding.ToolFilter))
                {
                    var filter = ParseToolFilter(binding.ToolFilter);
                    if (filter.Count > 0)
                    {
                        serverTools = serverTools
                            .Where(tool => filter.Contains(tool.Name))
                            .ToList();
                    }
                }

                tools.AddRange(serverTools.Select(tool => tool.Tool));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to load MCP tools for project agent {ProjectAgentRoleId} from server {ServerId}",
                    context.ProjectAgentRoleId,
                    binding.McpServerId);
            }
        }

        return tools;
    }

    /// <summary>
    /// 确保项目智能体绑定中允许使用指定工具。
    /// Ensures that the project-agent bindings allow the specified tools.
    /// </summary>
    public async Task<AgentMcpCapabilityGrantResult> EnsureToolsAllowedAsync(
        Guid projectAgentId,
        IReadOnlyCollection<string> requiredTools,
        CancellationToken ct)
    {
        var normalizedRequiredTools = requiredTools
            .Where(tool => !string.IsNullOrWhiteSpace(tool))
            .Select(tool => tool.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedRequiredTools.Count == 0)
            return new AgentMcpCapabilityGrantResult([], [], false);

        using var scope = _scopeFactory.CreateScope();
        var projectAgents = scope.ServiceProvider.GetRequiredService<IProjectAgentRoleRepository>();
        var agentRoleMcpBindings = scope.ServiceProvider.GetRequiredService<IAgentRoleMcpBindingRepository>();
        var repositoryContext = scope.ServiceProvider.GetRequiredService<IRepositoryContext>();

        var projectAgent = await projectAgents
            .AsNoTracking()
            .Include(agent => agent.Project)
            .FirstOrDefaultAsync(agent => agent.Id == projectAgentId, ct);
        if (projectAgent == null)
            return new AgentMcpCapabilityGrantResult([], normalizedRequiredTools, false);

        var bindings = await agentRoleMcpBindings
            .Include(binding => binding.McpServer)
            .Where(binding => binding.AgentRoleId == projectAgent.AgentRoleId && binding.IsEnabled && binding.McpServer!.IsEnabled)
            .ToListAsync(ct);

        var satisfied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var binding in bindings)
        {
            try
            {
                var configuration = await _configurationFileStore.GetProjectOverrideAsync(
                        binding.McpServerId,
                        projectAgent.Project?.WorkspacePath,
                        ct)
                    ?? await _configurationFileStore.GetOrCreateGlobalAsync(binding.McpServer!, ct);
                if (!configuration.IsEnabled)
                    continue;

                var connection = _resolvedConnectionFactory.CreateForProject(
                    binding.McpServer!,
                    projectAgent.ProjectId,
                    projectAgent.AgentRoleId,
                    projectAgent.Project?.WorkspacePath,
                    configuration);
                var serverTools = await _mcpHub.GetToolsAsync(connection, ct);
                var availableTools = serverTools
                    .Select(tool => tool.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var matchingTools = normalizedRequiredTools
                    .Where(availableTools.Contains)
                    .ToList();
                if (matchingTools.Count == 0)
                    continue;

                if (string.IsNullOrWhiteSpace(binding.ToolFilter))
                {
                    foreach (var tool in matchingTools)
                        satisfied.Add(tool);

                    continue;
                }

                var filter = ParseToolFilter(binding.ToolFilter);
                var added = false;
                foreach (var tool in matchingTools)
                {
                    satisfied.Add(tool);
                    if (filter.Add(tool))
                        added = true;
                }

                if (!added)
                    continue;

                // 这里只扩白名单，不会偷偷帮用户创建新的绑定。
                binding.ToolFilter = JsonSerializer.Serialize(filter.Order(StringComparer.OrdinalIgnoreCase).ToList());
                binding.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to inspect MCP tools for project agent {ProjectAgentRoleId} from server {ServerId}",
                    projectAgentId,
                    binding.McpServerId);
            }
        }

        if (changed)
            await repositoryContext.SaveChangesAsync(ct);

        var missing = normalizedRequiredTools
            .Where(tool => !satisfied.Contains(tool))
            .ToList();

        return new AgentMcpCapabilityGrantResult(
            SatisfiedTools: satisfied.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            MissingTools: missing,
            Changed: changed);
    }

    /// <summary>
    /// 将 MCP 工具白名单 JSON 解析为大小写不敏感的工具集合。
    /// Parses the MCP tool whitelist JSON into a case-insensitive tool set.
    /// </summary>
    /// <param name="toolFilterJson">存储在绑定中的工具过滤 JSON。/ Tool-filter JSON stored on the binding.</param>
    /// <returns>解析后的工具名称集合；无法解析时返回空集合。/ Parsed tool-name set, or an empty set when parsing fails.</returns>
    /// <remarks>
    /// zh-CN: 该解析逻辑会修剪空白项并吞掉 JSON 格式错误，从而把无效配置安全地退化为空白名单。
    /// en: This parser trims whitespace entries and swallows JSON format errors so invalid configuration safely degrades to an empty whitelist.
    /// </remarks>
    internal static HashSet<string> ParseToolFilter(string toolFilterJson)
    {
        try
        {
            var filter = JsonSerializer.Deserialize<string[]>(toolFilterJson)?
                .Where(tool => !string.IsNullOrWhiteSpace(tool))
                .Select(tool => NormalizeToolFilterEntry(tool.Trim()))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? [];

            if (filter.Contains("shell_exec"))
                filter.Add("shell_system_info");

            return filter;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeToolFilterEntry(string toolName)
        => string.Equals(toolName, "cmd", StringComparison.OrdinalIgnoreCase)
            ? "shell_exec"
            : string.Equals(toolName, "shell.exec", StringComparison.OrdinalIgnoreCase)
                ? "shell_exec"
                : string.Equals(toolName, "shell.system_info", StringComparison.OrdinalIgnoreCase)
                    ? "shell_system_info"
                    : toolName;
}


