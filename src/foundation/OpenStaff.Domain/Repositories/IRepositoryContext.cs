namespace OpenStaff.Repositories;

/// <summary>
/// Coordinates persistence commits across repositories that share the same unit of work.
/// New application code should depend on this contract instead of the aggregate repository bag.
/// </summary>
public interface IRepositoryContext
{
    /// <summary>
    /// Persists tracked changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
