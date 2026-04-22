using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.ApiServices;
/// <summary>
/// 非标准 CRUD API 应用服务的实现基类。
/// Base implementation for custom API-facing application services.
/// </summary>
/// <remarks>
/// zh-CN: 当服务属于编排、聚合查询或多资源工作流时，应继承此基类；仅对天然符合单聚合 CRUD 的服务使用 <see cref="CrudApiServiceBase{TEntity, TDto, TKey, TQueryInput, TCreateDto, TUpdateDto}"/>。
/// en: Inherit from this base when the service is orchestration-oriented, query-heavy, or spans multiple resources; reserve <see cref="CrudApiServiceBase{TEntity, TDto, TKey, TQueryInput, TCreateDto, TUpdateDto}"/> for services that naturally fit single-aggregate CRUD.
/// </remarks>
public abstract class ApiServiceBase : ServiceBase
{
    private static readonly IServiceProvider EmptyServiceProvider = new ServiceCollection().BuildServiceProvider();

    protected ApiServiceBase(IServiceProvider? serviceProvider = null) : base(serviceProvider ?? EmptyServiceProvider)
    {
    }
}


/// <summary>
/// 单聚合仓储驱动 CRUD API 应用服务的实现基类。
/// Base implementation for repository-backed single-aggregate CRUD API services.
/// </summary>
public abstract class CrudApiServiceBase<TEntity, TDto, TKey, TQueryInput, TCreateDto, TUpdateDto> : ApiServiceBase, ICrudApiServiceBase<TDto, TKey, TQueryInput, TCreateDto, TUpdateDto>
    where TEntity : class
{
    protected IRepository<TEntity, TKey> Repository { get; }
    protected IRepositoryContext RepositoryContext { get; }
    protected CrudApiServiceBase(
        IServiceProvider? serviceProvider,
        IRepository<TEntity, TKey> repository,
        IRepositoryContext repositoryContext) : base(serviceProvider)
    {
        Repository = repository;
        RepositoryContext = repositoryContext;
    }
    public virtual async Task<TDto?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, cancellationToken);

        return entity == null ? default : MapToDto(entity);
    }

    protected virtual TDto MapToDto(TEntity entity)
    {
        //TODO: using Riok.Mapperly
        throw new NotImplementedException();
    }
    protected virtual TEntity MapToEntity(TCreateDto input)
    {
        throw new NotImplementedException();
    }
    protected virtual TEntity MapToEntity(TUpdateDto input, TEntity entity)
    {
        throw new NotImplementedException();
    }

    protected virtual IQueryable<TEntity> ApplyFiltering(IQueryable<TEntity> queryable, TQueryInput input) { return queryable; }
    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> queryable, TQueryInput input) {
        if (typeof(TEntity).IsAssignableTo(typeof(IMustHaveCreatedAt)))
            queryable = queryable.OrderByDescending(e => ((IMustHaveCreatedAt)e).CreatedAt);
        return queryable;
    }
    protected virtual IQueryable<TEntity> ApplyPaging(IQueryable<TEntity> queryable, TQueryInput input)
    {
        if (input is IPagingInput pagingInput)
        {
            queryable = queryable.Skip((pagingInput.Page - 1) * pagingInput.PageSize).Take(pagingInput.PageSize);
        }
        return queryable;
    }

    protected virtual KeyNotFoundException CreateEntityNotFoundException(TKey id) =>
        new($"{typeof(TEntity).Name} '{id}' was not found.");

    public virtual async Task<PagedResult<TDto>> GetAllAsync(TQueryInput input, CancellationToken cancellationToken = default)
    {
        var queryable = Repository.GetQueryable();
        queryable = ApplyFiltering(queryable, input);
        var total = await queryable.CountAsync(cancellationToken);

        queryable = ApplySorting(queryable, input);
        queryable = ApplyPaging(queryable, input);

        var items = await queryable.ToListAsync(cancellationToken);
        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<TDto>(dtos, total);
    }

    public virtual async Task<TDto> CreateAsync(TCreateDto input, CancellationToken cancellationToken = default)
    {
        var create = MapToEntity(input);
        if (create is IMustHaveCreatedAt haveCreatedAt)
            haveCreatedAt.CreatedAt = DateTime.UtcNow;
        var entity = await Repository.AddAsync(create, cancellationToken);
        await RepositoryContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }


    public virtual async Task<TDto> UpdateAsync(TKey id, TUpdateDto input, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            throw CreateEntityNotFoundException(id);

        var update = MapToEntity(input, entity);
        if (update is IMayHaveUpdatedAt haveUpdatedAt)
            haveUpdatedAt.UpdatedAt = DateTime.UtcNow;
        entity = await Repository.UpdateAsync(update, cancellationToken);
        await RepositoryContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public virtual async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            throw CreateEntityNotFoundException(id);

        await Repository.DeleteAsync(entity, cancellationToken);
        await RepositoryContext.SaveChangesAsync(cancellationToken);
    }
}

