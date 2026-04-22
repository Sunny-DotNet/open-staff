using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Seeding.Services;

public sealed class JobTitleNormalizationSeedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobTitleNormalizationSeedService> _logger;

    public JobTitleNormalizationSeedService(
        IServiceScopeFactory scopeFactory,
        ILogger<JobTitleNormalizationSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var agentRoles = scope.ServiceProvider.GetRequiredService<IAgentRoleRepository>();
        var repositoryContext = scope.ServiceProvider.GetRequiredService<IRepositoryContext>();

        var roles = await agentRoles.GetQueryable()
            .Where(role => role.IsActive && !string.IsNullOrWhiteSpace(role.JobTitle))
            .ToListAsync(cancellationToken);

        var updatedCount = 0;
        foreach (var role in roles)
        {
            var normalizedKey = AgentJobTitleCatalog.NormalizeKey(role.JobTitle);
            if (string.IsNullOrWhiteSpace(normalizedKey)
                || string.Equals(role.JobTitle, normalizedKey, StringComparison.Ordinal))
            {
                continue;
            }

            role.JobTitle = normalizedKey;
            role.UpdatedAt = DateTime.UtcNow;
            updatedCount++;
        }

        if (updatedCount == 0)
            return;

        await repositoryContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Normalized {Count} agent-role job titles to stable keys.", updatedCount);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
