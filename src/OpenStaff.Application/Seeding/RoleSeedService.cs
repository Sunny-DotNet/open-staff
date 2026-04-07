using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent.Builtin;
using OpenStaff.Agent.Builtin.Roles;
using OpenStaff.Core.Agents;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Seeding;

/// <summary>
/// 启动时从嵌入资源加载内置角色到数据库
/// Seeds built-in roles from embedded resources into database on startup
/// </summary>
public class RoleSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoleSeedService> _logger;

    public RoleSeedService(IServiceProvider serviceProvider, ILogger<RoleSeedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var builtinProvider = scope.ServiceProvider.GetRequiredService<BuiltinAgentProvider>();
        var promptLoader = builtinProvider.PromptLoader;

        var roleConfigs = RoleConfigLoader.LoadBuiltin();
        var language = "zh-CN";

        foreach (var config in roleConfigs)
        {
            var fullPrompt = promptLoader.Load(config.SystemPrompt, language);

            var existing = await dbContext.AgentRoles
                .FirstOrDefaultAsync(r => r.RoleType == config.RoleType, cancellationToken);

            if (existing == null)
            {
                var role = new OpenStaff.Core.Models.AgentRole
                {
                    Id = Guid.NewGuid(),
                    Name = config.Name,
                    RoleType = config.RoleType,
                    Description = config.Description,
                    SystemPrompt = fullPrompt,
                    ModelName = config.ModelName,
                    Source = OpenStaff.Core.Models.AgentSource.Builtin,
                    IsBuiltin = true,
                    IsActive = true,
                    Config = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        modelParameters = config.ModelParameters,
                        tools = config.Tools,
                        routing = config.Routing
                    }),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbContext.AgentRoles.Add(role);
                _logger.LogInformation("Seeded built-in role: {RoleType} ({Name})", config.RoleType, config.Name);
            }
            else if (existing.IsBuiltin)
            {
                existing.SystemPrompt = fullPrompt;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            config.SystemPrompt = fullPrompt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
