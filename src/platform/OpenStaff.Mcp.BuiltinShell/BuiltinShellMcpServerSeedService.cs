using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Mcp.BuiltinShell;

internal sealed class BuiltinShellMcpServerSeedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BuiltinShellMcpServerSeedService> _logger;

    public BuiltinShellMcpServerSeedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BuiltinShellMcpServerSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var servers = scope.ServiceProvider.GetRequiredService<IMcpServerRepository>();
        var repositoryContext = scope.ServiceProvider.GetRequiredService<IRepositoryContext>();

        var existing = await servers
            .GetQueryable()
            .FirstOrDefaultAsync(
                server => server.Source == McpSources.Builtin
                          && server.TransportType == McpTransportTypes.Builtin
                          && server.Name == BuiltinShellMcpServerDefinition.ServerName,
                cancellationToken);

        if (existing == null)
        {
            servers.Add(BuiltinShellMcpServerDefinition.CreateServer());
            await repositoryContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded builtin MCP server '{ServerName}'.", BuiltinShellMcpServerDefinition.ServerName);
            return;
        }

        var changed = false;
        if (!string.Equals(existing.Description, BuiltinShellMcpServerDefinition.Description, StringComparison.Ordinal))
        {
            existing.Description = BuiltinShellMcpServerDefinition.Description;
            changed = true;
        }

        if (!string.Equals(existing.Category, McpCategories.DevTools, StringComparison.Ordinal))
        {
            existing.Category = McpCategories.DevTools;
            changed = true;
        }

        if (!string.Equals(existing.TransportType, McpTransportTypes.Builtin, StringComparison.Ordinal))
        {
            existing.TransportType = McpTransportTypes.Builtin;
            changed = true;
        }

        if (!string.Equals(existing.Mode, McpServerModes.Local, StringComparison.Ordinal))
        {
            existing.Mode = McpServerModes.Local;
            changed = true;
        }

        if (!string.Equals(existing.Source, McpSources.Builtin, StringComparison.Ordinal))
        {
            existing.Source = McpSources.Builtin;
            changed = true;
        }

        if (!string.Equals(existing.DefaultConfig, BuiltinShellMcpServerDefinition.DefaultConfigJson, StringComparison.Ordinal))
        {
            existing.DefaultConfig = BuiltinShellMcpServerDefinition.DefaultConfigJson;
            changed = true;
        }

        if (!changed)
            return;

        existing.UpdatedAt = DateTime.UtcNow;
        await repositoryContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated builtin MCP server '{ServerName}'.", BuiltinShellMcpServerDefinition.ServerName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
