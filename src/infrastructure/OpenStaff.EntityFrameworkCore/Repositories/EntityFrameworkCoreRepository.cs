using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.EntityFrameworkCore.Repositories;

/// <summary>
/// EF Core-backed repository that keeps DbSet-like query semantics while exposing the normalized repository contract.
/// </summary>
public class EntityFrameworkCoreRepository<TEntity, TKey>(AppDbContext dbContext) : IRepository<TEntity, TKey>
    where TEntity : class
{
    protected DbSet<TEntity> Set { get; } = dbContext.Set<TEntity>();
    public Type ElementType => ((IQueryable<TEntity>)Set).ElementType;
    public Expression Expression => ((IQueryable<TEntity>)Set).Expression;
    public IQueryProvider Provider => ((IQueryable<TEntity>)Set).Provider;

    public IEnumerator<TEntity> GetEnumerator() => ((IEnumerable<TEntity>)Set).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ValueTask<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default) =>
        Set.FindAsync([id!], cancellationToken);

    public ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default) =>
        Set.FindAsync(keyValues, cancellationToken);

    public ValueTask<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default) =>
        FindAsync(id, cancellationToken);

    public IQueryable<TEntity> GetQueryable() => Set.AsQueryable();

    public void Add(TEntity entity) => Set.Add(entity);
    public void AddRange(IEnumerable<TEntity> entities) => Set.AddRange(entities);
    public void AddRange(params TEntity[] entities) => Set.AddRange(entities);
    public void Remove(TEntity entity) => Set.Remove(entity);
    public void RemoveRange(IEnumerable<TEntity> entities) => Set.RemoveRange(entities);
    public void RemoveRange(params TEntity[] entities) => Set.RemoveRange(entities);

    public async ValueTask<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        return entity;
    }

    public ValueTask<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Update(entity);
        return ValueTask.FromResult(entity);
    }

    public async ValueTask DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(id, cancellationToken);
        if (entity is not null)
        {
            Set.Remove(entity);
        }
    }

    public ValueTask DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        return ValueTask.CompletedTask;
    }
}
