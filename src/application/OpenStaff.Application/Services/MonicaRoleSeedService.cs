using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Seeding.Services;

public sealed class MonicaRoleSeedService : IHostedService
{
    internal const string ResourceName = "OpenStaff.Application.Seeding.Roles.monica.role.json";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonicaRoleSeedService> _logger;

    public MonicaRoleSeedService(
        IServiceScopeFactory scopeFactory,
        ILogger<MonicaRoleSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var content = await ReadTemplateAsync();

        using var scope = _scopeFactory.CreateScope();
        var importService = scope.ServiceProvider.GetRequiredService<AgentRoleTemplateImportService>();
        var agentRoles = scope.ServiceProvider.GetRequiredService<IAgentRoleRepository>();

        var preview = await importService.PreviewAsync(content, cancellationToken);
        if (string.IsNullOrWhiteSpace(preview.Role.Name))
            throw new InvalidOperationException("The embedded Monica role template must declare a role name.");

        var existingRole = await FindExistingRoleAsync(agentRoles, preview.Role, cancellationToken);
        if (existingRole is not null)
        {
            _logger.LogInformation(
                "Skipped embedded Monica role seed because the role '{RoleName}' already exists.",
                existingRole.Name);
            return;
        }

        await importService.ImportAsync(content, overwriteExisting: false, cancellationToken);

        _logger.LogInformation("Seeded embedded Monica role '{RoleName}'.", preview.Role.Name);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<AgentRole?> FindExistingRoleAsync(
        IAgentRoleRepository agentRoles,
        AgentRoleTemplatePreviewDto preview,
        CancellationToken cancellationToken)
    {
        var externalId = ParseGuid(preview.ExternalId);
        if (externalId.HasValue)
        {
            var matchedById = await agentRoles.GetQueryable()
                .FirstOrDefaultAsync(role => role.Id == externalId.Value && role.IsActive, cancellationToken);
            if (matchedById is not null)
                return matchedById;
        }

        var activeRoles = await agentRoles.GetQueryable()
            .Where(role => role.IsActive)
            .ToListAsync(cancellationToken);

        return activeRoles.FirstOrDefault(role =>
            string.Equals(role.Name, preview.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid? ParseGuid(string? value)
        => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static async Task<string> ReadTemplateAsync()
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Monica role template '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
