using OpenStaff.AgentSouls.Services;
using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;

/// <summary>
/// Projects the platform soul catalogs into HTTP-facing DTOs.
/// </summary>
public sealed class AgentSoulApiService : ApiServiceBase, IAgentSoulApiService
{
    private readonly IAgentSoulService _agentSoulService;

    public AgentSoulApiService(IAgentSoulService agentSoulService, IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _agentSoulService = agentSoulService;
    }

    /// <inheritdoc />
    public async Task<AgentSoulCatalogDto> GetOptionsAsync(string? locale = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return new AgentSoulCatalogDto
        {
            Traits = await MapOptionsAsync(_agentSoulService.PersonalityTraits, locale),
            Attitudes = await MapOptionsAsync(_agentSoulService.WorkAttitudes, locale),
            Styles = await MapOptionsAsync(_agentSoulService.CommunicationStyles, locale)
        };
    }

    private static async Task<List<AgentSoulOptionDto>> MapOptionsAsync(IAgentSoulHttpService service, string? locale)
    {
        var items = await service.GetAllAsync();
        var results = new List<AgentSoulOptionDto>(items.Count);

        foreach (var item in items)
        {
            results.Add(new AgentSoulOptionDto
            {
                Key = item.Key,
                Label = await service.GetAsync(item.Key, locale)
            });
        }

        return results;
    }
}
