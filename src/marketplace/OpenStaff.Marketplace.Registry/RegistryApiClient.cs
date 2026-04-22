using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Marketplace.Registry;

/// <summary>
/// Registry API 客户端，封装对官方 MCP Registry 的 HTTP 调用。
/// Registry API client that encapsulates HTTP calls to the official MCP Registry.
/// </summary>
public class RegistryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RegistryApiClient> _logger;

    /// <summary>
    /// 官方 Registry 的默认基础地址。
    /// Default base URL of the official registry.
    /// </summary>
    public const string DefaultBaseUrl = "https://registry.modelcontextprotocol.io";

    /// <summary>
    /// 初始化 Registry API 客户端。
    /// Initializes the registry API client.
    /// </summary>
    /// <param name="httpClient">
    /// 用于访问 Registry 的 HTTP 客户端。
    /// HTTP client used to access the registry.
    /// </param>
    /// <param name="logger">
    /// 客户端日志记录器。
    /// Logger for the client.
    /// </param>
    public RegistryApiClient(HttpClient httpClient, ILogger<RegistryApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// 列出 Registry 中的 MCP Server。
    /// Lists MCP servers from the registry.
    /// </summary>
    /// <param name="cursor">
    /// 游标分页标记。
    /// Cursor used for pagination.
    /// </param>
    /// <param name="limit">
    /// 单次返回的最大记录数。
    /// Maximum number of records returned in a single request.
    /// </param>
    /// <param name="keyword">
    /// 关键词过滤；当前仅保留接口参数，实际过滤由调用方补充。
    /// Keyword filter; currently preserved as an API parameter while callers perform the actual filtering.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// Registry 响应模型。
    /// Registry response model.
    /// </returns>
    public async Task<RegistryResponse> ListServersAsync(
        string? cursor = null,
        int limit = 20,
        string? keyword = null,
        CancellationToken ct = default)
    {
        var queryParams = new List<string> { $"limit={limit}" };

        if (!string.IsNullOrWhiteSpace(cursor))
            queryParams.Add($"cursor={Uri.EscapeDataString(cursor)}");

        // zh-CN: 当前官方 API 还不支持关键字搜索参数，这里保留入参以便未来 API 升级时可以直接透传。
        // en: The official API does not support a keyword search parameter yet, but the argument is kept so future API upgrades can pass it through directly.
        _ = keyword;

        var url = $"/v0/servers?{string.Join('&', queryParams)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<RegistryResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ct);

            return result ?? new RegistryResponse();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch MCP Registry: {Url}", url);
            return new RegistryResponse();
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("MCP Registry request timed out: {Url}", url);
            return new RegistryResponse();
        }
    }
}
