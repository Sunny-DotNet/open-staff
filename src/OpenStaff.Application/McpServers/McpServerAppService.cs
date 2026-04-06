using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Contracts.McpServers;
using OpenStaff.Application.Contracts.McpServers.Dtos;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Application.McpServers;

public class McpServerAppService : IMcpServerAppService
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _encryption;
    private readonly McpClientManager _mcpClientManager;

    public McpServerAppService(AppDbContext db, EncryptionService encryption, McpClientManager mcpClientManager)
    {
        _db = db;
        _encryption = encryption;
        _mcpClientManager = mcpClientManager;
    }

    #region MCP Server 定义

    public async Task<List<McpServerDto>> GetAllServersAsync(GetAllServersRequest request, CancellationToken ct = default)
    {
        var query = _db.McpServers.AsNoTracking().Where(s => s.IsEnabled);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(s => s.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(s => s.Name.Contains(request.Search) || (s.Description != null && s.Description.Contains(request.Search)));

        var servers = await query.OrderBy(s => s.Name).ToListAsync(ct);

        // 批量查配置计数
        var serverIds = servers.Select(s => s.Id).ToList();
        var configCounts = await _db.McpServerConfigs
            .Where(c => serverIds.Contains(c.McpServerId))
            .GroupBy(c => c.McpServerId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return servers.Select(s => new McpServerDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Icon = s.Icon,
            Category = s.Category,
            TransportType = s.TransportType,
            Source = s.Source,
            DefaultConfig = s.DefaultConfig,
            Homepage = s.Homepage,
            NpmPackage = s.NpmPackage,
            PypiPackage = s.PypiPackage,
            IsEnabled = s.IsEnabled,
            ConfigCount = configCounts.GetValueOrDefault(s.Id, 0)
        }).ToList();
    }

    public async Task<McpServerDto?> GetServerByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.McpServers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return null;

        var configCount = await _db.McpServerConfigs.CountAsync(c => c.McpServerId == id, ct);
        return new McpServerDto
        {
            Id = s.Id, Name = s.Name, Description = s.Description, Icon = s.Icon,
            Category = s.Category, TransportType = s.TransportType, Source = s.Source,
            DefaultConfig = s.DefaultConfig, Homepage = s.Homepage,
            NpmPackage = s.NpmPackage, PypiPackage = s.PypiPackage,
            IsEnabled = s.IsEnabled, ConfigCount = configCount
        };
    }

    public async Task<McpServerDto> CreateServerAsync(CreateMcpServerInput input, CancellationToken ct = default)
    {
        var server = new McpServer
        {
            Name = input.Name,
            Description = input.Description,
            Icon = input.Icon,
            Category = input.Category,
            TransportType = input.TransportType,
            Source = McpSources.Custom,
            DefaultConfig = input.DefaultConfig,
            Homepage = input.Homepage,
            NpmPackage = input.NpmPackage,
            PypiPackage = input.PypiPackage
        };
        _db.McpServers.Add(server);
        await _db.SaveChangesAsync(ct);

        return new McpServerDto
        {
            Id = server.Id, Name = server.Name, Description = server.Description,
            Icon = server.Icon, Category = server.Category, TransportType = server.TransportType,
            Source = server.Source, DefaultConfig = server.DefaultConfig,
            Homepage = server.Homepage, NpmPackage = server.NpmPackage, PypiPackage = server.PypiPackage,
            IsEnabled = server.IsEnabled, ConfigCount = 0
        };
    }

    public async Task<bool> DeleteServerAsync(Guid id, CancellationToken ct = default)
    {
        var server = await _db.McpServers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (server == null) return false;
        if (server.Source == McpSources.Builtin) return false; // 内置不可删除
        _db.McpServers.Remove(server);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    #endregion

    #region MCP 配置实例

    public async Task<List<McpServerConfigDto>> GetConfigsByServerAsync(Guid mcpServerId, CancellationToken ct = default)
    {
        return await _db.McpServerConfigs.AsNoTracking()
            .Include(c => c.McpServer)
            .Where(c => c.McpServerId == mcpServerId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => ToConfigDto(c))
            .ToListAsync(ct);
    }

    public async Task<List<McpServerConfigDto>> GetAllConfigsAsync(CancellationToken ct = default)
    {
        return await _db.McpServerConfigs.AsNoTracking()
            .Include(c => c.McpServer)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => ToConfigDto(c))
            .ToListAsync(ct);
    }

    public async Task<McpServerConfigDto?> GetConfigByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.McpServerConfigs.AsNoTracking()
            .Include(c => c.McpServer)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return c == null ? null : ToConfigDto(c);
    }

    public async Task<McpServerConfigDto> CreateConfigAsync(CreateMcpServerConfigInput input, CancellationToken ct = default)
    {
        var config = new McpServerConfig
        {
            McpServerId = input.McpServerId,
            Name = input.Name,
            Description = input.Description,
            TransportType = input.TransportType,
            ConnectionConfig = input.ConnectionConfig,
            EnvironmentVariables = !string.IsNullOrEmpty(input.EnvironmentVariables)
                ? _encryption.Encrypt(input.EnvironmentVariables) : null,
            AuthConfig = !string.IsNullOrEmpty(input.AuthConfig)
                ? _encryption.Encrypt(input.AuthConfig) : null
        };
        _db.McpServerConfigs.Add(config);
        await _db.SaveChangesAsync(ct);

        var server = await _db.McpServers.FindAsync([config.McpServerId], ct);
        return new McpServerConfigDto
        {
            Id = config.Id, McpServerId = config.McpServerId,
            McpServerName = server?.Name ?? "", Name = config.Name,
            Description = config.Description, TransportType = config.TransportType,
            ConnectionConfig = config.ConnectionConfig,
            IsEnabled = config.IsEnabled, CreatedAt = config.CreatedAt
        };
    }

    public async Task<McpServerConfigDto?> UpdateConfigAsync(Guid id, UpdateMcpServerConfigInput input, CancellationToken ct = default)
    {
        var config = await _db.McpServerConfigs.Include(c => c.McpServer).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (config == null) return null;

        if (input.Name != null) config.Name = input.Name;
        if (input.Description != null) config.Description = input.Description;
        if (input.TransportType != null) config.TransportType = input.TransportType;
        if (input.ConnectionConfig != null) config.ConnectionConfig = input.ConnectionConfig;
        if (input.EnvironmentVariables != null)
            config.EnvironmentVariables = _encryption.Encrypt(input.EnvironmentVariables);
        if (input.AuthConfig != null)
            config.AuthConfig = _encryption.Encrypt(input.AuthConfig);
        if (input.IsEnabled.HasValue) config.IsEnabled = input.IsEnabled.Value;

        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToConfigDto(config);
    }

    public async Task<bool> DeleteConfigAsync(Guid id, CancellationToken ct = default)
    {
        var config = await _db.McpServerConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (config == null) return false;
        _db.McpServerConfigs.Remove(config);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    #endregion

    #region 测试连接

    public async Task<TestMcpConnectionResult> TestConnectionAsync(Guid configId, CancellationToken ct = default)
    {
        var config = await _db.McpServerConfigs.FirstOrDefaultAsync(x => x.Id == configId, ct);
        if (config == null)
            return new TestMcpConnectionResult { Success = false, Message = "配置不存在" };

        try
        {
            var tools = await _mcpClientManager.ListToolsAsync(config, ct);
            return new TestMcpConnectionResult
            {
                Success = true,
                Message = $"连接成功，发现 {tools.Count} 个工具",
                Tools = tools.Select(t => new McpToolDto
                {
                    Name = t.Name,
                    Description = t.Description,
                    InputSchema = t.JsonSchema.ToString()
                }).ToList()
            };
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

    #endregion

    #region 员工 MCP 绑定

    public async Task<List<AgentMcpBindingDto>> GetAgentBindingsAsync(Guid agentRoleId, CancellationToken ct = default)
    {
        return await _db.AgentRoleMcpConfigs.AsNoTracking()
            .Where(b => b.AgentRoleId == agentRoleId)
            .Include(b => b.McpServerConfig!)
                .ThenInclude(c => c.McpServer)
            .Select(b => new AgentMcpBindingDto
            {
                McpServerConfigId = b.McpServerConfigId,
                McpServerConfigName = b.McpServerConfig!.Name,
                McpServerName = b.McpServerConfig.McpServer!.Name,
                Icon = b.McpServerConfig.McpServer.Icon,
                ToolFilter = b.ToolFilter
            })
            .ToListAsync(ct);
    }

    public async Task SetAgentBindingsAsync(SetAgentBindingsRequest request, CancellationToken ct = default)
    {
        var existing = await _db.AgentRoleMcpConfigs
            .Where(b => b.AgentRoleId == request.AgentRoleId)
            .ToListAsync(ct);
        _db.AgentRoleMcpConfigs.RemoveRange(existing);

        var newBindings = request.McpServerConfigIds.Select(configId => new AgentRoleMcpConfig
        {
            AgentRoleId = request.AgentRoleId,
            McpServerConfigId = configId
        });
        _db.AgentRoleMcpConfigs.AddRange(newBindings);
        await _db.SaveChangesAsync(ct);
    }

    #endregion

    private static McpServerConfigDto ToConfigDto(McpServerConfig c) => new()
    {
        Id = c.Id,
        McpServerId = c.McpServerId,
        McpServerName = c.McpServer?.Name ?? "",
        Name = c.Name,
        Description = c.Description,
        TransportType = c.TransportType,
        ConnectionConfig = c.ConnectionConfig,
        EnvironmentVariables = null, // 不返回加密内容
        IsEnabled = c.IsEnabled,
        CreatedAt = c.CreatedAt
    };
}
