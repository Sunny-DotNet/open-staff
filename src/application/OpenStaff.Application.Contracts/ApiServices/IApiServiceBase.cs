using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// API 应用服务统一标记契约。
/// Marker contract for all API-facing application services.
/// </summary>
/// <remarks>
/// zh-CN: 不满足标准单聚合 CRUD 形状的应用服务（例如编排、运行态查询、文件浏览、会话流转）应继承此接口，以便仍然纳入统一规则集。
/// en: Application services that do not naturally match single-aggregate CRUD (for example orchestration, runtime queries, file browsing, or session flows) should inherit this interface so they still participate in the common rule set.
/// </remarks>
public interface IApiServiceBase
{
}

/// <summary>
/// 面向单聚合标准 CRUD 的 API 应用服务契约。
/// Standard API service contract for single-aggregate CRUD workloads.
/// </summary>
/// <remarks>
/// zh-CN: 仅当服务天然符合 GetById/GetAll/Create/Update/Delete 这一单实体仓储形状时，才应继承此接口；否则应退回 <see cref="IApiServiceBase"/> 并保留自定义操作面。
/// en: Inherit from this interface only when the service naturally matches the GetById/GetAll/Create/Update/Delete shape over a single repository-backed aggregate; otherwise inherit from <see cref="IApiServiceBase"/> and keep a custom surface.
/// </remarks>
public interface ICrudApiServiceBase<TDto, TKey, TQueryInput, TCreateDto, TUpdateDto> : IApiServiceBase
{
    Task<TDto?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<PagedResult<TDto>> GetAllAsync(TQueryInput input,CancellationToken cancellationToken = default);
    Task<TDto> CreateAsync(TCreateDto input, CancellationToken cancellationToken = default);
    Task<TDto> UpdateAsync(TKey id, TUpdateDto input, CancellationToken cancellationToken = default);
    Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
}

