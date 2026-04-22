namespace OpenStaff.Repositories;

/// <summary>
/// Main repository contract. New code should pair repositories with <see cref="IRepositoryContext"/>.
/// </summary>
public interface IRepository<TEntity, TKey>
    : IQueryable<TEntity>
    where TEntity : class
{
    ValueTask<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default);
    ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default);
    ValueTask<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    IQueryable<TEntity> GetQueryable();
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void AddRange(params TEntity[] entities);
    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
    void RemoveRange(params TEntity[] entities);
    ValueTask<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    ValueTask<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(TKey id, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}
