using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Application.Projects.Services;

namespace OpenStaff.Application.Seeding.Services;

/// <summary>
/// zh-CN: 启动时补齐角色默认 MCP/Skill，并回填历史项目成员缺失的绑定。
/// en: Seeds default role MCP/skill bindings and backfills missing project-agent bindings at startup.
/// </summary>
public sealed class RoleCapabilitySeedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RoleCapabilitySeedService> _logger;

    public RoleCapabilitySeedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RoleCapabilitySeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var bindingService = scope.ServiceProvider.GetRequiredService<RoleCapabilityBindingService>();

        var seededRoleBindings = await bindingService.SeedDefaultRoleBindingsAsync(cancellationToken);
        var seededProjectBindings = await bindingService.SeedMissingProjectAgentBindingsAsync(cancellationToken);

        if (seededRoleBindings > 0 || seededProjectBindings > 0)
        {
            _logger.LogInformation(
                "Seeded {RoleBindingCount} role capability bindings and {ProjectBindingCount} project capability bindings",
                seededRoleBindings,
                seededProjectBindings);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
