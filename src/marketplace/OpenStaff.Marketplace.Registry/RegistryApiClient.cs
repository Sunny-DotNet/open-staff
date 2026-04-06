using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Marketplace.Registry;

/// <summary>
/// Registry API 客户端 — 封装对 registry.modelcontextprotocol.io 的 HTTP 调用
/// </summary>
public class RegistryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RegistryApiClient> _logger;

    public const string DefaultBaseUrl = "https://registry.modelcontextprotocol.io";

    public RegistryApiClient(HttpClient httpClient, ILogger<RegistryApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// 列出 MCP Server（支持游标分页和关键词过滤）
    /// </summary>
    public async Task<RegistryResponse> ListServersAsync(
        string? cursor = null,
        int limit = 20,
        string? keyword = null,
        CancellationToken ct = default)
    {
        var queryParams = new List<string> { $"limit={limit}" };

        if (!string.IsNullOrWhiteSpace(cursor))
            queryParams.Add($"cursor={Uri.EscapeDataString(cursor)}");

        // Registry API 目前不支持 keyword 参数，但预留
        // 如果后续支持可以加上: queryParams.Add($"q={Uri.EscapeDataString(keyword)}");

        var url = $"/v0/servers?{string.Join('&', queryParams)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<RegistryResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

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
