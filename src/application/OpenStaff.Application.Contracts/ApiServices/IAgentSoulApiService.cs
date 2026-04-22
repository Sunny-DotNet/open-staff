using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;

/// <summary>
/// 灵魂配置目录应用服务。
/// Application service that exposes soul-option catalogs to clients.
/// </summary>
public interface IAgentSoulApiService : IApiServiceBase
{
    /// <summary>获取灵魂配置选项。 / Gets soul-option catalog groups.</summary>
    Task<AgentSoulCatalogDto> GetOptionsAsync(string? locale = null, CancellationToken ct = default);
}
