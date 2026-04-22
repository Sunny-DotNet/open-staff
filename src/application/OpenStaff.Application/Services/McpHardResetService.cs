using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Repositories;

namespace OpenStaff.Application.McpServers.Services;

/// <summary>
/// Performs a one-time destructive reset of legacy MCP state so the application starts from the new template/profile model only.
/// </summary>
public sealed class McpHardResetService : IHostedService
{
    private const string ResetMarkerFileName = ".mcp-hard-reset-v3";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpDataDirectoryLayout _layout;
    private readonly ILogger<McpHardResetService> _logger;

    public McpHardResetService(
        IServiceScopeFactory scopeFactory,
        IMcpDataDirectoryLayout layout,
        ILogger<McpHardResetService> logger)
    {
        _scopeFactory = scopeFactory;
        _layout = layout;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dataRoot = _layout.GetDataRoot();
        Directory.CreateDirectory(dataRoot);
        var markerPath = Path.Combine(dataRoot, ResetMarkerFileName);
        if (File.Exists(markerPath))
            return;

        using var scope = _scopeFactory.CreateScope();
        var mcpServers = scope.ServiceProvider.GetRequiredService<IMcpServerRepository>();
        var roleBindings = scope.ServiceProvider.GetRequiredService<IAgentRoleMcpBindingRepository>();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var repositoryContext = scope.ServiceProvider.GetRequiredService<IRepositoryContext>();

        var roleBindingRows = await roleBindings.GetQueryable().ToListAsync(cancellationToken);
        var serverRows = await mcpServers.GetQueryable().ToListAsync(cancellationToken);
        var projectWorkspacePaths = await projects.GetQueryable()
            .Where(project => project.WorkspacePath != null)
            .Select(project => project.WorkspacePath!)
            .ToListAsync(cancellationToken);

        if (roleBindingRows.Count > 0)
            roleBindings.RemoveRange(roleBindingRows);
        if (serverRows.Count > 0)
            mcpServers.RemoveRange(serverRows);

        await repositoryContext.SaveChangesAsync(cancellationToken);

        foreach (var entry in Directory.EnumerateFileSystemEntries(dataRoot))
        {
            if (Directory.Exists(entry))
                Directory.Delete(entry, recursive: true);
            else
                File.Delete(entry);
        }

        foreach (var workspacePath in projectWorkspacePaths)
        {
            var mcpDirectory = Path.Combine(workspacePath, ".mcp");
            if (Directory.Exists(mcpDirectory))
                Directory.Delete(mcpDirectory, recursive: true);
        }

        await File.WriteAllTextAsync(markerPath, "reset", cancellationToken);
        _logger.LogInformation(
            "Reset legacy MCP state: removed {ServerCount} servers and {RoleBindingCount} role bindings.",
            serverRows.Count,
            roleBindingRows.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
