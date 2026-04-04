using Microsoft.EntityFrameworkCore;
using OpenStaff.Agents.Roles;
using OpenStaff.Core.Agents;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Services;

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
        var agentFactory = scope.ServiceProvider.GetRequiredService<OpenStaff.Agents.AgentFactory>();
        var promptLoader = scope.ServiceProvider.GetRequiredService<IPromptLoader>();

        var roleConfigs = RoleConfigLoader.LoadAll();
        var language = "zh-CN"; // 默认语言

        foreach (var config in roleConfigs)
        {
            // 加载嵌入资源的完整提示词文本
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
                // 内置角色每次启动时更新提示词（跟随嵌入资源更新）
                existing.SystemPrompt = fullPrompt;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            // 同时把完整文本更新到 RoleConfig，供 AgentFactory 使用
            config.SystemPrompt = fullPrompt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 注册内置 RoleConfig（已含完整提示词）
        foreach (var config in roleConfigs)
        {
            agentFactory.RegisterRole(config);
        }

        // 将所有 DB 角色注册到 AgentFactory（包含 ModelProviderId 等运行时信息）
        var allRoles = await dbContext.AgentRoles.Where(r => r.IsActive).ToListAsync(cancellationToken);
        foreach (var dbRole in allRoles)
        {
            agentFactory.RegisterDbRole(dbRole);
            _logger.LogDebug("Registered DB role: {RoleType} (ProviderId={ProviderId})",
                dbRole.RoleType, dbRole.ModelProviderId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
