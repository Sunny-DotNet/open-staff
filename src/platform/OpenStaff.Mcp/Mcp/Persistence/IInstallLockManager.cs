namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 安装锁接口，防止相同安装目标被并发写入。
/// en: Install-lock contract that prevents concurrent writes to the same install target.
/// </summary>
public interface IInstallLockManager
{
    Task<IAsyncDisposable> AcquireAsync(string lockName, CancellationToken cancellationToken = default);
}
