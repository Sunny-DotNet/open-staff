using System.Text;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 使用锁文件 + 独占文件句柄的方式提供跨进程安装保护。
/// en: Provides cross-process install protection by combining lock files with exclusive file handles.
/// </summary>
public sealed class FileInstallLockManager : IInstallLockManager
{
    private readonly IMcpDataDirectoryLayout _layout;

    public FileInstallLockManager(IMcpDataDirectoryLayout layout)
    {
        _layout = layout;
    }

    public async Task<IAsyncDisposable> AcquireAsync(string lockName, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_layout.GetLocksDirectory(), $"{PathSegmentSanitizer.Sanitize(lockName)}.lock");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stream = new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.ProcessId.ToString()), cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return new FileLockHandle(stream);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
    }

    private sealed class FileLockHandle : IAsyncDisposable
    {
        private readonly FileStream _stream;

        public FileLockHandle(FileStream stream)
        {
            _stream = stream;
        }

        public async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync();
        }
    }
}
