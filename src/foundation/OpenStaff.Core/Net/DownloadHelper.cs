using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Net;

public static class DownloadHelper
{

    /// <summary>
    /// 下载并缓存 Copilot 模型目录，在缓存仍然新鲜时直接复用本地副本。
    /// Downloads and caches the Copilot model catalog, reusing the local copy while it is still considered fresh.
    /// </summary>
    /// <param name="url">
    /// 需要下载的模型目录地址。
    /// Model catalog URL to download.
    /// </param>
    /// <param name="filename">
    /// 本地缓存文件路径。
    /// Local cache file path.
    /// </param>
    /// <param name="ex">
    /// 缓存有效期。
    /// Cache freshness window.
    /// </param>
    /// <param name="httpClientFactory">
    /// 用于构造已附带认证头的 HTTP 客户端工厂。
    /// Factory that creates an HTTP client with the required authentication headers already applied.
    /// </param>
    /// <param name="cancellationToken">
    /// 取消下载流程的令牌。
    /// Token used to cancel the download flow.
    /// </param>
    public static async Task DownloadUseCachedAsync(
        string url,
        string filename,
        TimeSpan ex,
        Func<HttpClient> httpClientFactory,
        CancellationToken cancellationToken,
        ILoggerFactory? loggerFactory = null)
    {
        var logger = loggerFactory?.CreateLogger(nameof(DownloadHelper));

        // zh-CN: 如果本地缓存仍在有效期内，直接复用缓存，避免每次查询模型列表都重新访问 Copilot API。
        // en: Reuse the local cache while it is still fresh so model discovery does not hit the Copilot API on every request.
        if (File.Exists(filename))
        {
            var lastWriteTime = File.GetLastWriteTime(filename);
            if (DateTime.Now - lastWriteTime < ex)
            {
                return;
            }
        }

        // zh-CN: 下载前确保缓存目录存在，避免首次运行时因目录缺失而写入失败。
        // en: Ensure the cache directory exists before downloading so first-run writes do not fail.
        var directory = Path.GetDirectoryName(filename);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            var httpClient = httpClientFactory();

            // zh-CN: 采用流式复制响应内容，减少大响应在内存中的临时占用。
            // en: Stream the response directly to disk to avoid holding large payloads in memory.
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await response.Content.CopyToAsync(fileStream, cancellationToken);

            File.SetLastWriteTime(filename, DateTime.Now);
        }
        catch (Exception e)
        {
            // zh-CN: 如果下载失败但旧缓存还在，则回退到旧缓存，保证模型列表查询具备一定的离线容错能力。
            // en: If the download fails but an old cache exists, fall back to that cache so model lookup retains some offline resilience.
            if (File.Exists(filename))
            {
                logger?.LogWarning(e, "下载失败，回退使用过期的缓存文件: {Url}", url);
                return;
            }

            logger?.LogError(e, "下载文件失败且无可用缓存: {Url}", url);
            throw;
        }
    }

}
