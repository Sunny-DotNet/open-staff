using OpenStaff.Application.McpServers.Services;
using System.Text.Json.Nodes;
using OpenStaff.Mcp;

namespace OpenStaff.ApiServices;
/// <summary>
/// Unified MCP application service implementation.
/// </summary>
public class McpServerApiService : ApiServiceBase, IMcpServerApiService
{
    private static readonly JsonSerializerOptions DraftJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IMcpServerRepository _mcpServers;
    private readonly IAgentRoleMcpBindingRepository _agentRoleMcpBindings;
    private readonly IAgentRoleRepository _agentRoles;
    private readonly IProjectAgentRoleRepository _projectAgents;
    private readonly IRepositoryContext _repositoryContext;
    private readonly IMcpConfigurationFileStore _configurationFileStore;
    private readonly McpResolvedConnectionFactory _resolvedConnectionFactory;
    private readonly McpHub _mcpHub;
    private readonly McpWarmupCoordinator? _mcpWarmupCoordinator;
    private readonly McpRuntimeParameterDefaultsService _runtimeParameterDefaults;
    private readonly McpStructuredMetadataFactory _structuredMetadataFactory;
    private readonly McpProfileConnectionRenderer _profileConnectionRenderer;
    private readonly IReadOnlyList<IMcpCatalogSource> _catalogSources;
    private readonly IMcpCatalogService _mcpCatalogService;
    private readonly IMcpInstallationService _mcpInstallationService;
    private readonly IInstalledMcpService _installedMcpService;
    private readonly IMcpRuntimeResolver _mcpRuntimeResolver;
    private readonly IMcpUninstallService _mcpUninstallService;
    private readonly IMcpRepairService _mcpRepairService;

    public McpServerApiService(
        IMcpServerRepository mcpServers,
        IAgentRoleMcpBindingRepository agentRoleMcpBindings,
        IAgentRoleRepository agentRoles,
        IProjectAgentRoleRepository projectAgents,
        IRepositoryContext repositoryContext,
        IMcpConfigurationFileStore configurationFileStore,
        McpResolvedConnectionFactory resolvedConnectionFactory,
        McpHub mcpHub,
        McpRuntimeParameterDefaultsService runtimeParameterDefaults,
        McpStructuredMetadataFactory structuredMetadataFactory,
        McpProfileConnectionRenderer profileConnectionRenderer,
        IEnumerable<IMcpCatalogSource> catalogSources,
        IMcpCatalogService mcpCatalogService,
        IMcpInstallationService mcpInstallationService,
        IInstalledMcpService installedMcpService,
        IMcpRuntimeResolver mcpRuntimeResolver,
        IMcpUninstallService mcpUninstallService,
        IMcpRepairService mcpRepairService,
        McpWarmupCoordinator? mcpWarmupCoordinator = null,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _mcpServers = mcpServers;
        _agentRoleMcpBindings = agentRoleMcpBindings;
        _agentRoles = agentRoles;
        _projectAgents = projectAgents;
        _repositoryContext = repositoryContext;
        _configurationFileStore = configurationFileStore;
        _resolvedConnectionFactory = resolvedConnectionFactory;
        _mcpHub = mcpHub;
        _mcpWarmupCoordinator = mcpWarmupCoordinator;
        _runtimeParameterDefaults = runtimeParameterDefaults;
        _structuredMetadataFactory = structuredMetadataFactory;
        _profileConnectionRenderer = profileConnectionRenderer;
        _catalogSources = catalogSources.OrderBy(source => source.Priority).ToList();
        _mcpCatalogService = mcpCatalogService;
        _mcpInstallationService = mcpInstallationService;
        _installedMcpService = installedMcpService;
        _mcpRuntimeResolver = mcpRuntimeResolver;
        _mcpUninstallService = mcpUninstallService;
        _mcpRepairService = mcpRepairService;
    }

    public Task<List<McpSourceDto>> GetSourcesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_catalogSources
            .Select(source => new McpSourceDto
            {
                SourceKey = source.SourceKey,
                DisplayName = source.DisplayName
            })
            .ToList());
    }

    public async Task<McpCatalogSearchResultDto> SearchCatalogAsync(McpCatalogSearchQueryDto query, CancellationToken ct = default)
    {
        var result = await _mcpCatalogService.SearchCatalogAsync(new CatalogSearchQuery
        {
            SourceKey = query.SourceKey,
            Keyword = query.Keyword,
            Category = query.Category,
            TransportType = ParseTransportType(query.TransportType),
            Cursor = query.Cursor,
            Page = query.Page,
            PageSize = query.PageSize
        }, ct);

        var installedIndex = await BuildInstalledCatalogIndexAsync(ct);

        return new McpCatalogSearchResultDto
        {
            Items = result.Items.Select(entry => ToCatalogEntryDto(entry, installedIndex)).ToList(),
            TotalCount = result.TotalCount,
            NextCursor = result.NextCursor
        };
    }

    public async Task<McpCatalogEntryDto?> GetCatalogEntryAsync(string sourceKey, string entryId, CancellationToken ct = default)
    {
        try
        {
            var entry = await _mcpCatalogService.GetCatalogEntryAsync(sourceKey, entryId, ct);
            var installedIndex = await BuildInstalledCatalogIndexAsync(ct);
            return ToCatalogEntryDto(entry, installedIndex);
        }
        catch (CatalogEntryNotFoundException)
        {
            return null;
        }
    }

    public async Task<McpServerDto> InstallAsync(InstallMcpServerInput input, CancellationToken ct = default)
    {
        var catalogEntry = await _mcpCatalogService.GetCatalogEntryAsync(input.SourceKey, input.CatalogEntryId, ct);
        var installed = await _mcpInstallationService.InstallAsync(new InstallRequest
        {
            SourceKey = input.SourceKey,
            CatalogEntryId = input.CatalogEntryId,
            SelectedChannelId = input.SelectedChannelId,
            RequestedVersion = input.RequestedVersion,
            OverwriteExisting = input.OverwriteExisting
        }, ct);

        var runtime = await _mcpRuntimeResolver.ResolveRuntimeAsync(installed.InstallId, ct);
        var selectedChannel = ResolveInstallChannel(catalogEntry, installed, input.SelectedChannelId);
        var server = await UpsertInstalledServerAsync(catalogEntry, selectedChannel, installed, runtime, input.Name, ct);
        await _mcpHub.InvalidateServerAsync(server.Id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(server.Id, ct);

        return await GetServerByIdAsync(server.Id, ct)
            ?? throw new KeyNotFoundException($"MCP server '{server.Id}' was not found after install.");
    }

    public async Task<McpUninstallCheckResultDto> CheckUninstallAsync(Guid serverId, CancellationToken ct = default)
    {
        var server = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == serverId, ct)
            ?? throw new KeyNotFoundException($"MCP server '{serverId}' was not found.");

        var installInfo = McpManagedInstallInfo.TryParse(server.InstallInfo);
        if (installInfo?.InstallId is Guid installId)
            return ToUninstallCheckDto(await _mcpUninstallService.CheckUninstallAsync(installId, ct));

        return await BuildLocalDeleteCheckAsync(server, ct);
    }

    public async Task<List<McpServerDto>> GetAllServersAsync(GetAllServersRequest request, CancellationToken ct = default)
    {
        var query = _mcpServers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Source))
            query = query.Where(server => server.Source == request.Source);

        if (request.EnabledState.HasValue)
            query = query.Where(server => server.IsEnabled == request.EnabledState.Value);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(server => server.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(server =>
                server.Name.Contains(request.Search) ||
                (server.Description != null && server.Description.Contains(request.Search)));
        }

        var servers = await query.OrderBy(server => server.Name).ToListAsync(ct);
        if (servers.Count == 0)
            return [];

        var installedIndex = await BuildInstalledByIdAsync(ct);

        var dtos = servers
            .Select(server => ToServerDto(server, 1, installedIndex))
            .Where(server => MatchesInstalledState(server, request.InstalledState))
            .ToList();

        return dtos;
    }

    public async Task<McpServerDto?> GetServerByIdAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (server == null)
            return null;

        var installedIndex = await BuildInstalledByIdAsync(ct);
        return ToServerDto(server, 1, installedIndex);
    }

    public async Task<McpServerDto> CreateCustomServerAsync(CreateMcpServerInput input, CancellationToken ct = default)
    {
        var server = new McpServer
        {
            Name = input.Name,
            Description = input.Description,
            Icon = input.Icon,
            Category = input.Category,
            TransportType = input.TransportType,
            Mode = string.IsNullOrWhiteSpace(input.Mode) ? McpServerModes.Local : input.Mode,
            Source = McpSources.Custom,
            DefaultConfig = input.TemplateJson,
            Homepage = input.Homepage,
            NpmPackage = input.NpmPackage,
            PypiPackage = input.PypiPackage
        };

        _mcpServers.Add(server);
        await _repositoryContext.SaveChangesAsync(ct);

        return await GetServerByIdAsync(server.Id, ct)
            ?? throw new KeyNotFoundException($"MCP server '{server.Id}' was not found after creation.");
    }

    public async Task<McpServerDto?> UpdateServerAsync(Guid id, UpdateMcpServerInput input, CancellationToken ct = default)
    {
        var server = await _mcpServers.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (server == null)
            return null;

        if (input.Name != null) server.Name = input.Name;
        if (input.Description != null) server.Description = input.Description;
        if (input.Icon != null) server.Icon = input.Icon;
        if (input.Category != null) server.Category = input.Category;
        if (input.TransportType != null) server.TransportType = input.TransportType;
        if (input.Mode != null) server.Mode = input.Mode;
        if (input.TemplateJson != null) server.DefaultConfig = input.TemplateJson;
        if (input.Homepage != null) server.Homepage = input.Homepage;
        if (input.NpmPackage != null) server.NpmPackage = input.NpmPackage;
        if (input.PypiPackage != null) server.PypiPackage = input.PypiPackage;
        if (input.IsEnabled.HasValue) server.IsEnabled = input.IsEnabled.Value;

        server.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);
        await _mcpHub.InvalidateServerAsync(id);
        _mcpWarmupCoordinator?.ForgetServer(id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(id, ct);
        return await GetServerByIdAsync(id, ct);
    }

    public async Task<DeleteMcpServerResultDto> DeleteServerAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _mcpServers.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException($"MCP server '{id}' was not found.");

        if (string.Equals(server.Source, McpSources.Builtin, StringComparison.OrdinalIgnoreCase))
        {
            var check = await BuildLocalDeleteCheckAsync(server, ct);
            check.CanUninstall = false;
            check.BlockingReasons.Insert(0, "Built-in MCP servers cannot be deleted.");
            return ToDeleteBlockedResult(server.Id, null, check, "Built-in MCP servers cannot be deleted.");
        }

        var installInfo = McpManagedInstallInfo.TryParse(server.InstallInfo);
        if (installInfo?.InstallId is Guid installId)
        {
            var uninstallCheck = await _mcpUninstallService.CheckUninstallAsync(installId, ct);
            if (!uninstallCheck.CanUninstall)
                return ToDeleteBlockedResult(server.Id, installId, ToUninstallCheckDto(uninstallCheck), "Managed install is still referenced.");

            await DeleteServerConfigurationFilesAsync(server.Id, ct);
            await _mcpUninstallService.UninstallAsync(installId, ct);
            _mcpServers.Remove(server);
            await _repositoryContext.SaveChangesAsync(ct);
            await _mcpHub.InvalidateServerAsync(server.Id);
            if (_mcpWarmupCoordinator != null)
                await _mcpWarmupCoordinator.RebuildServerAsync(server.Id, ct);

            return new DeleteMcpServerResultDto
            {
                ServerId = server.Id,
                InstallId = installId,
                Deleted = true,
                Uninstalled = true,
                Action = "uninstalled",
                Message = "Managed MCP install and server definition were removed."
            };
        }

        var localCheck = await BuildLocalDeleteCheckAsync(server, ct);
        if (!localCheck.CanUninstall)
            return ToDeleteBlockedResult(server.Id, null, localCheck, "Server definition is still referenced.");

        await DeleteServerConfigurationFilesAsync(server.Id, ct);
        _mcpServers.Remove(server);
        await _repositoryContext.SaveChangesAsync(ct);
        await _mcpHub.InvalidateServerAsync(server.Id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(server.Id, ct);

        return new DeleteMcpServerResultDto
        {
            ServerId = server.Id,
            Deleted = true,
            Uninstalled = false,
            Action = "deleted-definition",
            Message = "Server definition was removed."
        };
    }

    public async Task<McpRepairResultDto> RepairInstallAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _mcpServers.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException($"MCP server '{id}' was not found.");

        var currentInfo = McpManagedInstallInfo.TryParse(server.InstallInfo);
        if (currentInfo?.InstallId is not Guid installId)
            throw new InvalidOperationException("Only managed MCP installs can be repaired.");

        var repair = await _mcpRepairService.RepairInstallAsync(installId, ct);

        RuntimeSpec? runtime = null;
        if (repair.Repaired)
            runtime = await _mcpRuntimeResolver.ResolveRuntimeAsync(installId, ct);

        server.TransportType = runtime != null
            ? ToEntityTransportType(runtime.TransportType)
            : currentInfo.TransportType ?? server.TransportType;
        if (runtime != null)
        {
            server.Mode = runtime.TransportType == OpenStaff.Mcp.Models.McpTransportType.Stdio
                ? McpServerModes.Local
                : McpServerModes.Remote;
        }
        server.InstallInfo = BuildManagedInstallInfoJson(repair.InstalledMcp, runtime, currentInfo?.ChannelId, currentInfo);
        server.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);
        await _mcpHub.InvalidateServerAsync(server.Id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(server.Id, ct);

        return new McpRepairResultDto
        {
            Repaired = repair.Repaired,
            Message = repair.Message,
            Server = await GetServerByIdAsync(server.Id, ct)
                ?? throw new KeyNotFoundException($"MCP server '{server.Id}' was not found after repair.")
        };
    }

    public async Task SetServerEnabledAsync(Guid id, bool isEnabled, CancellationToken ct = default)
    {
        var server = await _mcpServers.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new KeyNotFoundException($"MCP server '{id}' was not found.");

        server.IsEnabled = isEnabled;
        server.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);
        await _mcpHub.InvalidateServerAsync(id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(id, ct);
    }

    public async Task<List<McpServerConfigDto>> GetConfigsByServerAsync(Guid mcpServerId, CancellationToken ct = default)
    {
        var config = await GetConfigByIdAsync(mcpServerId, ct);
        return config == null ? [] : [config];
    }

    public async Task<List<McpServerConfigDto>> GetAllConfigsAsync(CancellationToken ct = default)
    {
        var servers = await _mcpServers.AsNoTracking()
            .Where(server => server.IsEnabled)
            .OrderBy(server => server.Name)
            .ToListAsync(ct);

        var configs = new List<McpServerConfigDto>(servers.Count);
        foreach (var server in servers)
        {
            configs.Add(await BuildConfigDtoAsync(server, ct));
        }

        return configs;
    }

    public async Task<McpServerConfigDto?> GetConfigByIdAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return server == null ? null : await BuildConfigDtoAsync(server, ct);
    }

    public async Task<McpServerConfigDto> CreateConfigAsync(CreateMcpServerConfigInput input, CancellationToken ct = default)
    {
        var server = await _mcpServers.FirstOrDefaultAsync(item => item.Id == input.McpServerId, ct)
            ?? throw new KeyNotFoundException($"MCP server '{input.McpServerId}' was not found.");

        await _configurationFileStore.SaveGlobalAsync(
            server,
            input.SelectedProfileId,
            input.ParameterValues,
            isEnabled: true,
            ct);
        await _mcpHub.InvalidateServerAsync(server.Id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(server.Id, ct);
        return await BuildConfigDtoAsync(server, ct);
    }

    public async Task<McpServerConfigDto?> UpdateConfigAsync(Guid id, UpdateMcpServerConfigInput input, CancellationToken ct = default)
    {
        var server = await _mcpServers.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (server == null)
            return null;

        var current = await _configurationFileStore.GetOrCreateGlobalAsync(server, ct);
        await _configurationFileStore.SaveGlobalAsync(
            server,
            input.SelectedProfileId ?? current.SelectedProfileId,
            input.ParameterValues ?? current.ParameterValues.ToJsonString(),
            input.IsEnabled ?? current.IsEnabled,
            ct);
        await _mcpHub.InvalidateServerAsync(id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(id, ct);
        return await BuildConfigDtoAsync(server, ct);
    }

    public async Task<bool> DeleteConfigAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (server == null)
            return false;

        await _configurationFileStore.DeleteGlobalAsync(id, ct);
        await _mcpHub.InvalidateServerAsync(id);
        _mcpWarmupCoordinator?.ForgetServer(id);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildServerAsync(id, ct);
        return true;
    }

    public async Task<TestMcpConnectionResult> TestConnectionAsync(Guid configId, CancellationToken ct = default)
    {
        var server = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == configId, ct);
        if (server == null)
            return new TestMcpConnectionResult { Success = false, Message = "配置不存在" };

        try
        {
            var configuration = await _configurationFileStore.GetOrCreateGlobalAsync(server, ct);
            var connection = _resolvedConnectionFactory.CreateForAgentRole(server, Guid.Empty, configuration);
            var tools = await _mcpHub.GetToolsAsync(connection, ct);
            return BuildTestConnectionSuccessResult(tools);
        }
        catch (Exception ex)
        {
            return new TestMcpConnectionResult
            {
                Success = false,
                Message = $"连接失败: {ex.Message}"
            };
        }
    }

    public async Task<TestMcpConnectionResult> TestConnectionDraftAsync(TestMcpConnectionDraftInput input, CancellationToken ct = default)
    {
        try
        {
            var server = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == input.McpServerId, ct)
                ?? throw new KeyNotFoundException($"MCP server '{input.McpServerId}' was not found.");
            var tools = await _mcpHub.GetDraftToolsAsync(
                server,
                ResolveSelectedProfileId(server, input.SelectedProfileId),
                input.ParameterValues,
                ct);

            return BuildTestConnectionSuccessResult(tools);
        }
        catch (Exception ex)
        {
            return new TestMcpConnectionResult
            {
                Success = false,
                Message = $"连接失败: {ex.Message}"
            };
        }
    }

    public async Task<List<AgentRoleMcpBindingDto>> GetAgentRoleBindingsAsync(Guid agentRoleId, CancellationToken ct = default)
    {
        var bindings = await _agentRoleMcpBindings.AsNoTracking()
            .Where(binding => binding.AgentRoleId == agentRoleId)
            .Include(binding => binding.McpServer)
            .OrderBy(binding => binding.CreatedAt)
            .ToListAsync(ct);

        return bindings.Select(ToAgentRoleBindingDto).ToList();
    }

    public async Task ReplaceAgentRoleBindingsAsync(ReplaceAgentRoleMcpBindingsRequest request, CancellationToken ct = default)
    {
        var roleExists = await _agentRoles.AsNoTracking()
            .AnyAsync(role => role.Id == request.AgentRoleId && role.IsActive, ct);
        if (!roleExists)
            throw new KeyNotFoundException($"Agent role '{request.AgentRoleId}' was not found.");

        var normalizedBindings = request.Bindings
            .GroupBy(binding => binding.McpServerId)
            .Select(group => group.Last())
            .ToList();

        var existingBindings = await _agentRoleMcpBindings
            .Where(binding => binding.AgentRoleId == request.AgentRoleId)
            .ToListAsync(ct);
        _agentRoleMcpBindings.RemoveRange(existingBindings);

        foreach (var binding in normalizedBindings)
        {
            _ = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == binding.McpServerId, ct)
                ?? throw new KeyNotFoundException($"MCP server '{binding.McpServerId}' was not found.");
            _agentRoleMcpBindings.Add(new AgentRoleMcpBinding
            {
                AgentRoleId = request.AgentRoleId,
                McpServerId = binding.McpServerId,
                ToolFilter = binding.ToolFilter,
                IsEnabled = binding.IsEnabled
            });
        }

        await _repositoryContext.SaveChangesAsync(ct);
        await _mcpHub.InvalidateAgentRoleAsync(request.AgentRoleId);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildAgentRoleAsync(request.AgentRoleId, ct);
    }

    public async Task<McpBindingDraftDto> CreateBindingDraftAsync(CreateMcpBindingDraftInput input, CancellationToken ct = default)
    {
        var server = await _mcpServers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == input.McpServerId && item.IsEnabled, ct)
            ?? throw new KeyNotFoundException($"MCP server '{input.McpServerId}' was not found.");

        var normalizedScope = input.Scope?.Trim();
        string parameterValues;

        if (string.Equals(normalizedScope, McpBindingDraftScopes.AgentRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedScope, McpBindingDraftScopes.AgentRoleTest, StringComparison.OrdinalIgnoreCase))
        {
            if (!input.AgentRoleId.HasValue)
                throw new ArgumentException("AgentRoleId is required for agent-role test drafts.", nameof(input));

            var roleExists = await _agentRoles.AsNoTracking()
                .AnyAsync(role => role.Id == input.AgentRoleId.Value && role.IsActive, ct);
            if (!roleExists)
                throw new KeyNotFoundException($"Agent role '{input.AgentRoleId}' was not found.");

            parameterValues = BuildParameterValuesDraft(server, projectWorkspacePath: null, projectScoped: false);
        }
        else if (string.Equals(normalizedScope, McpBindingDraftScopes.ProjectAgentRole, StringComparison.OrdinalIgnoreCase))
        {
            if (!input.ProjectAgentRoleId.HasValue)
                throw new ArgumentException("ProjectAgentRoleId is required for project-agent drafts.", nameof(input));

            var projectAgent = await _projectAgents.AsNoTracking()
                .Include(agent => agent.Project)
                .FirstOrDefaultAsync(agent => agent.Id == input.ProjectAgentRoleId.Value, ct)
                ?? throw new KeyNotFoundException($"Project agent '{input.ProjectAgentRoleId}' was not found.");

            parameterValues = BuildParameterValuesDraft(server, projectAgent.Project?.WorkspacePath, projectScoped: true);
        }
        else
        {
            throw new ArgumentException($"Unsupported MCP binding draft scope '{input.Scope}'.", nameof(input));
        }

        return new McpBindingDraftDto
        {
            McpServerId = server.Id,
            ToolFilter = null,
            SelectedProfileId = ResolveSelectedProfileId(server, null),
            ParameterValues = parameterValues,
            IsEnabled = true
        };
    }

    public async Task<List<ProjectAgentMcpBindingDto>> GetProjectAgentBindingsAsync(Guid projectAgentId, CancellationToken ct = default)
    {
        var projectAgent = await _projectAgents.AsNoTracking()
            .Include(agent => agent.Project)
            .FirstOrDefaultAsync(agent => agent.Id == projectAgentId, ct)
            ?? throw new KeyNotFoundException($"Project agent '{projectAgentId}' was not found.");

        var roleBindings = await _agentRoleMcpBindings.AsNoTracking()
            .Include(binding => binding.McpServer)
            .Where(binding => binding.AgentRoleId == projectAgent.AgentRoleId)
            .OrderBy(binding => binding.CreatedAt)
            .ToListAsync(ct);

        var result = new List<ProjectAgentMcpBindingDto>(roleBindings.Count);
        foreach (var binding in roleBindings)
        {
            var projectOverride = await _configurationFileStore.GetProjectOverrideAsync(
                binding.McpServerId,
                projectAgent.Project?.WorkspacePath,
                ct);
            var effectiveConfiguration = projectOverride
                ?? _configurationFileStore.CreateProjectDefault(binding.McpServer!, projectAgent.Project?.WorkspacePath);
            result.Add(ToProjectAgentBindingDto(projectAgentId, binding, effectiveConfiguration));
        }

        return result;
    }

    public async Task ReplaceProjectAgentBindingsAsync(ReplaceProjectAgentMcpBindingsRequest request, CancellationToken ct = default)
    {
        var projectAgent = await _projectAgents.AsNoTracking()
            .Include(agent => agent.Project)
            .FirstOrDefaultAsync(agent => agent.Id == request.ProjectAgentRoleId, ct);
        if (projectAgent == null)
            throw new KeyNotFoundException($"Project agent '{request.ProjectAgentRoleId}' was not found.");

        var normalizedBindings = request.Bindings
            .GroupBy(binding => binding.McpServerId)
            .Select(group => group.Last())
            .ToList();

        foreach (var binding in normalizedBindings)
        {
            var server = await _mcpServers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == binding.McpServerId, ct)
                ?? throw new KeyNotFoundException($"MCP server '{binding.McpServerId}' was not found.");
            await _configurationFileStore.SaveProjectOverrideAsync(
                server,
                projectAgent.Project?.WorkspacePath
                    ?? throw new InvalidOperationException($"Project '{projectAgent.ProjectId}' workspace is not initialized."),
                binding.SelectedProfileId,
                binding.ParameterValues,
                binding.IsEnabled,
                ct);
        }

        await _mcpHub.InvalidateProjectAsync(projectAgent.ProjectId);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildProjectAsync(projectAgent.ProjectId, ct);
    }

    private async Task<McpServer> UpsertInstalledServerAsync(
        CatalogEntry catalogEntry,
        InstallChannel? installChannel,
        InstalledMcp installed,
        RuntimeSpec runtime,
        string? overrideName,
        CancellationToken ct)
    {
        var server = await FindManagedServerAsync(installed.InstallId, installed.SourceKey, installed.CatalogEntryId, ct);
        if (server == null)
        {
            server = new McpServer
            {
                Source = McpSources.Marketplace
            };
            _mcpServers.Add(server);
        }

        server.Name = string.IsNullOrWhiteSpace(overrideName)
            ? catalogEntry.DisplayName
            : overrideName.Trim();
        server.Description = catalogEntry.Description;
        server.Category = catalogEntry.Category ?? McpCategories.General;
        server.TransportType = ToEntityTransportType(runtime.TransportType);
        server.Mode = runtime.TransportType == OpenStaff.Mcp.Models.McpTransportType.Stdio
            ? McpServerModes.Local
            : McpServerModes.Remote;
        server.Homepage = catalogEntry.Homepage;
        server.MarketplaceUrl = catalogEntry.RepositoryUrl;
        if (installChannel?.Metadata.TryGetValue(McpSourceMetadataKeys.RawTemplateJson, out var rawTemplateJson) == true
            && !string.IsNullOrWhiteSpace(rawTemplateJson))
        {
            server.DefaultConfig = rawTemplateJson;
        }
        server.InstallInfo = BuildManagedInstallInfoJson(installed, runtime, installChannel?.ChannelId);
        server.NpmPackage = installChannel?.ChannelType == OpenStaff.Mcp.Models.McpChannelType.Npm
            ? installChannel.PackageIdentifier
            : null;
        server.PypiPackage = installChannel?.ChannelType == OpenStaff.Mcp.Models.McpChannelType.Pypi
            ? installChannel.PackageIdentifier
            : null;
        server.IsEnabled = true;
        server.UpdatedAt = DateTime.UtcNow;

        await _repositoryContext.SaveChangesAsync(ct);
        return server;
    }

    private async Task<McpServer?> FindManagedServerAsync(Guid installId, string sourceKey, string catalogEntryId, CancellationToken ct)
    {
        var candidates = await _mcpServers
            .Where(server => server.InstallInfo != null)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(server =>
        {
            var info = McpManagedInstallInfo.TryParse(server.InstallInfo);
            return info?.InstallId == installId
                   || (string.Equals(info?.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(info?.CatalogEntryId, catalogEntryId, StringComparison.OrdinalIgnoreCase));
        });
    }

    private async Task<Dictionary<Guid, InstalledMcp>> BuildInstalledByIdAsync(CancellationToken ct)
    {
        return (await _installedMcpService.ListInstalledAsync(ct))
            .ToDictionary(item => item.InstallId, item => item);
    }

    private async Task<Dictionary<string, InstalledMcp>> BuildInstalledCatalogIndexAsync(CancellationToken ct)
    {
        return (await _installedMcpService.ListInstalledAsync(ct))
            .GroupBy(item => $"{item.SourceKey}|{item.CatalogEntryId}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);
    }

    private static McpCatalogEntryDto ToCatalogEntryDto(
        CatalogEntry entry,
        IReadOnlyDictionary<string, InstalledMcp> installedIndex)
    {
        installedIndex.TryGetValue($"{entry.SourceKey}|{entry.EntryId}", out var installed);

        return new McpCatalogEntryDto
        {
            EntryId = entry.EntryId,
            SourceKey = entry.SourceKey,
            Name = entry.Name,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            Category = entry.Category,
            Version = entry.Version,
            Homepage = entry.Homepage,
            RepositoryUrl = entry.RepositoryUrl,
            TransportTypes = entry.TransportTypes.Select(ToApiTransportType).ToList(),
            InstallChannels = entry.InstallChannels.Select(ToInstallChannelDto).ToList(),
            IsInstalled = entry.IsInstalled,
            InstalledState = entry.InstalledState?.ToString(),
            InstalledVersion = entry.InstalledVersion,
            InstallId = installed?.InstallId
        };
    }

    private McpServerDto ToServerDto(
        McpServer server,
        int configCount,
        IReadOnlyDictionary<Guid, InstalledMcp> installedIndex)
    {
        var installInfo = McpManagedInstallInfo.TryParse(server.InstallInfo);
        InstalledMcp? installed = null;
        if (installInfo?.InstallId is Guid installId)
            installedIndex.TryGetValue(installId, out installed);
        var structuredMetadata = _structuredMetadataFactory.Build(server);

        return new McpServerDto
        {
            Id = server.Id,
            Name = server.Name,
            Description = server.Description,
            Icon = server.Icon,
            Logo = structuredMetadata.Logo,
            Category = server.Category,
            TransportType = server.TransportType,
            Mode = server.Mode,
            Source = server.Source,
            TemplateJson = server.DefaultConfig,
            InstallId = installInfo?.InstallId,
            CatalogEntryId = installInfo?.CatalogEntryId,
            InstallSourceKey = installInfo?.SourceKey,
            InstallChannelId = installInfo?.ChannelId,
            InstallChannelType = installInfo?.ChannelType,
            InstalledVersion = installed?.Version ?? installInfo?.InstalledVersion,
            InstalledState = installed?.InstallState.ToString() ?? installInfo?.InstallState,
            InstallDirectory = installed?.InstallDirectory ?? installInfo?.InstallDirectory,
            ManifestPath = installed?.ManifestPath ?? installInfo?.ManifestPath,
            LastInstallError = installed?.LastError ?? installInfo?.LastError,
            IsManagedInstall = installInfo?.IsManagedInstall ?? false,
            Homepage = server.Homepage,
            NpmPackage = server.NpmPackage,
            PypiPackage = server.PypiPackage,
            IsEnabled = server.IsEnabled,
            ConfigCount = configCount,
            DefaultProfileId = structuredMetadata.DefaultProfileId,
            Profiles = structuredMetadata.Profiles.ToList(),
            ParameterSchema = structuredMetadata.ParameterSchema.ToList()
        };
    }

    private static McpInstallChannelDto ToInstallChannelDto(InstallChannel channel) => new()
    {
        ChannelId = channel.ChannelId,
        ChannelType = channel.ChannelType.ToStorageValue(),
        TransportType = ToApiTransportType(channel.TransportType),
        Version = channel.Version,
        EntrypointHint = channel.EntrypointHint,
        PackageIdentifier = channel.PackageIdentifier,
        ArtifactUrl = channel.ArtifactUrl,
        Metadata = new Dictionary<string, string>(channel.Metadata, StringComparer.OrdinalIgnoreCase)
    };

    private async Task<McpUninstallCheckResultDto> BuildLocalDeleteCheckAsync(McpServer server, CancellationToken ct)
    {
        var roleBindings = await _agentRoleMcpBindings.AsNoTracking()
            .Where(binding => binding.McpServerId == server.Id)
            .Include(binding => binding.AgentRole)
            .Select(binding => binding.AgentRole!.Name)
            .ToListAsync(ct);

        var projectAgents = await _projectAgents.AsNoTracking()
            .Include(agent => agent.Project)
            .Where(agent => agent.Project != null)
            .ToListAsync(ct);
        var projectBindings = new List<string>();
        foreach (var agent in projectAgents)
        {
            var overrideConfig = await _configurationFileStore.GetProjectOverrideAsync(server.Id, agent.Project?.WorkspacePath, ct);
            if (overrideConfig?.Exists == true)
                projectBindings.Add(agent.Project!.Name);
        }

        var blockingReasons = new List<string>();
        if (projectBindings.Count > 0)
            blockingReasons.Add("Project MCP overrides still reference this server.");
        if (roleBindings.Count > 0)
            blockingReasons.Add("Agent-role bindings still reference this server.");

        return new McpUninstallCheckResultDto
        {
            CanUninstall = blockingReasons.Count == 0,
            BlockingReasons = blockingReasons,
            ReferencedByConfigs = [],
            ReferencedByProjectBindings = projectBindings
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ReferencedByRoleBindings = roleBindings
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static McpUninstallCheckResultDto ToUninstallCheckDto(UninstallCheckResult result) => new()
    {
        CanUninstall = result.CanUninstall,
        BlockingReasons = [.. result.BlockingReasons],
        ReferencedByConfigs = [.. result.ReferencedByConfigs],
        ReferencedByProjectBindings = [.. result.ReferencedByProjectBindings],
        ReferencedByRoleBindings = [.. result.ReferencedByRoleBindings]
    };

    private static DeleteMcpServerResultDto ToDeleteBlockedResult(
        Guid serverId,
        Guid? installId,
        McpUninstallCheckResultDto check,
        string message)
    {
        return new DeleteMcpServerResultDto
        {
            ServerId = serverId,
            InstallId = installId,
            Deleted = false,
            Uninstalled = false,
            Action = "blocked",
            Message = message,
            BlockingReasons = check.BlockingReasons,
            ReferencedByConfigs = check.ReferencedByConfigs,
            ReferencedByProjectBindings = check.ReferencedByProjectBindings,
            ReferencedByRoleBindings = check.ReferencedByRoleBindings
        };
    }

    private static TestMcpConnectionResult BuildTestConnectionSuccessResult(IReadOnlyList<McpRuntimeToolDescriptor> tools)
    {
        return new TestMcpConnectionResult
        {
            Success = true,
            Message = $"连接成功，发现 {tools.Count} 个工具",
            Tools = tools.Select(tool => new McpToolDto
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = tool.InputSchema ?? "{}"
            }).ToList()
        };
    }

    private static bool MatchesInstalledState(McpServerDto server, string? installedStateFilter)
    {
        if (string.IsNullOrWhiteSpace(installedStateFilter))
            return true;

        if (string.Equals(installedStateFilter, "managed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(installedStateFilter, "installed", StringComparison.OrdinalIgnoreCase))
        {
            return server.IsManagedInstall;
        }

        if (string.Equals(installedStateFilter, "not-installed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(installedStateFilter, "none", StringComparison.OrdinalIgnoreCase))
        {
            return !server.IsManagedInstall;
        }

        return string.Equals(server.InstalledState, installedStateFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static InstallChannel? ResolveInstallChannel(
        CatalogEntry entry,
        InstalledMcp installed,
        string? selectedChannelId)
    {
        if (!string.IsNullOrWhiteSpace(selectedChannelId))
        {
            return entry.InstallChannels.FirstOrDefault(channel =>
                string.Equals(channel.ChannelId, selectedChannelId, StringComparison.OrdinalIgnoreCase));
        }

        return entry.InstallChannels.FirstOrDefault(channel =>
            string.Equals(channel.ChannelType.ToStorageValue(), installed.ChannelType.ToStorageValue(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(ToApiTransportType(channel.TransportType), ToApiTransportType(installed.TransportType), StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(channel.Version)
                || string.Equals(channel.Version, installed.Version, StringComparison.OrdinalIgnoreCase)));
    }

    private static McpTransportType? ParseTransportType(string? transportType)
    {
        if (string.IsNullOrWhiteSpace(transportType))
            return null;

        return transportType.Trim().ToLowerInvariant() switch
        {
            "stdio" => OpenStaff.Mcp.Models.McpTransportType.Stdio,
            "http" => OpenStaff.Mcp.Models.McpTransportType.Http,
            "sse" => OpenStaff.Mcp.Models.McpTransportType.Sse,
            "streamable-http" => OpenStaff.Mcp.Models.McpTransportType.StreamableHttp,
            _ => null
        };
    }

    private static string ToApiTransportType(McpTransportType transportType) => transportType switch
    {
        OpenStaff.Mcp.Models.McpTransportType.Stdio => McpTransportTypes.Stdio,
        OpenStaff.Mcp.Models.McpTransportType.Http => McpTransportTypes.Http,
        OpenStaff.Mcp.Models.McpTransportType.Sse => McpTransportTypes.Sse,
        OpenStaff.Mcp.Models.McpTransportType.StreamableHttp => McpTransportTypes.StreamableHttp,
        _ => McpTransportTypes.Stdio
    };

    private static string ToEntityTransportType(McpTransportType transportType) => ToApiTransportType(transportType);

    private static string BuildManagedInstallInfoJson(
        InstalledMcp installed,
        RuntimeSpec? runtime,
        string? channelId,
        McpManagedInstallInfo? fallback = null)
    {
        var info = new McpManagedInstallInfo
        {
            InstallId = installed.InstallId,
            CatalogEntryId = installed.CatalogEntryId,
            SourceKey = installed.SourceKey,
            ChannelId = channelId ?? fallback?.ChannelId,
            ChannelType = installed.ChannelType.ToStorageValue(),
            InstalledVersion = installed.Version,
            InstallState = installed.InstallState.ToString(),
            InstallDirectory = installed.InstallDirectory,
            ManifestPath = installed.ManifestPath,
            LastError = installed.LastError,
            TransportType = runtime != null ? ToApiTransportType(runtime.TransportType) : fallback?.TransportType,
            Url = runtime?.Url ?? fallback?.Url,
            Headers = runtime != null
                ? new Dictionary<string, string?>(runtime.Headers, StringComparer.OrdinalIgnoreCase)
                : fallback?.Headers ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Command = runtime?.Command ?? fallback?.Command,
            Args = runtime != null ? [.. runtime.Arguments] : fallback?.Args ?? [],
            EnvironmentVariables = runtime != null
                ? new Dictionary<string, string?>(runtime.EnvironmentVariables, StringComparer.OrdinalIgnoreCase)
                : fallback?.EnvironmentVariables ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            WorkingDirectory = runtime?.WorkingDirectory ?? fallback?.WorkingDirectory
        };

        return JsonSerializer.Serialize(info);
    }

    private async Task DeleteServerConfigurationFilesAsync(Guid serverId, CancellationToken ct)
    {
        await _configurationFileStore.DeleteGlobalAsync(serverId, ct);

        var workspacePaths = await _projectAgents.AsNoTracking()
            .Include(agent => agent.Project)
            .Where(agent => agent.Project != null)
            .Select(agent => agent.Project!.WorkspacePath)
            .Distinct()
            .ToListAsync(ct);
        await _configurationFileStore.DeleteProjectOverridesAsync(serverId, workspacePaths, ct);
    }

    private AgentRoleMcpBindingDto ToAgentRoleBindingDto(AgentRoleMcpBinding binding)
    {
        return new AgentRoleMcpBindingDto
        {
            Id = binding.Id,
            AgentRoleId = binding.AgentRoleId,
            McpServerId = binding.McpServerId,
            McpServerName = binding.McpServer?.Name ?? string.Empty,
            Icon = binding.McpServer?.Icon,
            Mode = binding.McpServer?.Mode ?? McpServerModes.Local,
            TransportType = binding.McpServer?.TransportType ?? McpTransportTypes.Stdio,
            ToolFilter = binding.ToolFilter,
            SelectedProfileId = null,
            ParameterValues = null,
            IsEnabled = binding.IsEnabled
        };
    }

    private ProjectAgentMcpBindingDto ToProjectAgentBindingDto(
        Guid projectAgentRoleId,
        AgentRoleMcpBinding binding,
        McpStoredConfiguration configuration)
    {
        return new ProjectAgentMcpBindingDto
        {
            Id = binding.Id,
            ProjectAgentRoleId = projectAgentRoleId,
            McpServerId = binding.McpServerId,
            McpServerName = binding.McpServer?.Name ?? string.Empty,
            Icon = binding.McpServer?.Icon,
            Mode = binding.McpServer?.Mode ?? McpServerModes.Local,
            TransportType = binding.McpServer?.TransportType ?? McpTransportTypes.Stdio,
            ToolFilter = binding.ToolFilter,
            SelectedProfileId = configuration.SelectedProfileId,
            ParameterValues = configuration.ParameterValues.ToJsonString(DraftJsonOptions),
            IsEnabled = binding.IsEnabled
        };
    }

    private string BuildParameterValuesDraft(McpServer server, string? projectWorkspacePath, bool projectScoped)
    {
        var runtimeParameters = projectScoped
            ? _runtimeParameterDefaults.CreateProjectDefaults(server, projectWorkspacePath)
            : _runtimeParameterDefaults.CreateHostDefaults(server);

        return runtimeParameters.ToJsonString(DraftJsonOptions);
    }

    private async Task<McpServerConfigDto> BuildConfigDtoAsync(McpServer server, CancellationToken ct)
    {
        var configuration = await _configurationFileStore.GetOrCreateGlobalAsync(server, ct);

        return new McpServerConfigDto
        {
            Id = server.Id,
            McpServerId = server.Id,
            McpServerName = server.Name,
            Name = $"{server.Name} · Global",
            Description = "Global MCP configuration",
            TransportType = _profileConnectionRenderer.ResolveTransportType(server, configuration.SelectedProfileId, server.TransportType),
            SelectedProfileId = configuration.SelectedProfileId,
            ParameterValues = configuration.ParameterValues.ToJsonString(DraftJsonOptions),
            HasEnvironmentVariables = false,
            HasAuthConfig = false,
            IsEnabled = configuration.IsEnabled,
            CreatedAt = configuration.UpdatedAt ?? server.CreatedAt
        };
    }

    private string? ResolveSelectedProfileId(McpServer? server, string? requestedProfileId)
        => server == null
            ? requestedProfileId
            : _profileConnectionRenderer.ResolveSelectedProfileId(server, requestedProfileId);

}



