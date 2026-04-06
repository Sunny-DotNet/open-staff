using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.ModelData;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/models-dev")]
public class ModelDataController : ControllerBase
{
    private readonly IModelDataAppService _modelDataAppService;

    public ModelDataController(IModelDataAppService modelDataAppService)
    {
        _modelDataAppService = modelDataAppService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
        => Ok(await _modelDataAppService.GetStatusAsync(ct));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
        => Ok(await _modelDataAppService.RefreshAsync(ct));

    [HttpGet("providers/{providerKey}/models")]
    public async Task<IActionResult> GetModels(string providerKey, CancellationToken ct)
        => Ok(await _modelDataAppService.GetModelsAsync(providerKey, ct));

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
        => Ok(await _modelDataAppService.GetProvidersAsync(ct));
}
