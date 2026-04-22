using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;

public interface ITalentMarketApiService : IApiServiceBase
{
    Task<List<TalentMarketSourceDto>> GetSourcesAsync(CancellationToken ct = default);

    Task<TalentMarketSearchResultDto> SearchAsync(TalentMarketSearchQueryDto query, CancellationToken ct = default);

    Task<TalentMarketHirePreviewDto> PreviewHireAsync(PreviewTalentMarketHireInput input, CancellationToken ct = default);

    Task<ImportAgentRoleTemplateResultDto> HireAsync(HireTalentMarketRoleInput input, CancellationToken ct = default);
}
