using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/talent-market")]
public class TalentMarketController : ControllerBase
{
    private readonly ITalentMarketApiService _talentMarketApiService;

    public TalentMarketController(ITalentMarketApiService talentMarketApiService)
    {
        _talentMarketApiService = talentMarketApiService;
    }

    [HttpGet("sources")]
    public async Task<ActionResult<List<TalentMarketSourceDto>>> GetSources(CancellationToken ct)
        => Ok(await _talentMarketApiService.GetSourcesAsync(ct));

    [HttpGet("search")]
    public async Task<ActionResult<TalentMarketSearchResultDto>> Search([FromQuery] TalentMarketSearchQueryDto query, CancellationToken ct)
        => Ok(await _talentMarketApiService.SearchAsync(query, ct));

    [HttpPost("preview-hire")]
    public async Task<ActionResult<TalentMarketHirePreviewDto>> PreviewHire([FromBody] PreviewTalentMarketHireInput input, CancellationToken ct)
        => Ok(await _talentMarketApiService.PreviewHireAsync(input, ct));

    [HttpPost("hire")]
    public async Task<ActionResult<ImportAgentRoleTemplateResultDto>> Hire([FromBody] HireTalentMarketRoleInput input, CancellationToken ct)
        => Ok(await _talentMarketApiService.HireAsync(input, ct));
}
