using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 基于 HttpClient 的安装产物下载器，并支持可选的 SHA-256 校验。
/// en: HttpClient-based artifact downloader with optional SHA-256 verification.
/// </summary>
public sealed class HttpClientArtifactDownloader : IArtifactDownloader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenStaffMcpOptions _options;

    public HttpClientArtifactDownloader(IHttpClientFactory httpClientFactory, IOptions<OpenStaffMcpOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task DownloadAsync(Uri artifactUri, string destinationPath, string? checksum = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        var client = _httpClientFactory.CreateClient(nameof(OpenStaffMcpModule));
        using var request = new HttpRequestMessage(HttpMethod.Get, artifactUri);
        foreach (var header in _options.DefaultRequestHeaders)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var destination = File.Create(destinationPath))
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(checksum))
            return;

        await using var downloadedStream = File.OpenRead(destinationPath);
        var hash = await SHA256.HashDataAsync(downloadedStream, cancellationToken);
        var actual = Convert.ToHexString(hash);
        var normalizedExpected = checksum.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(actual, normalizedExpected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Checksum mismatch for '{artifactUri}'. Expected '{normalizedExpected}', actual '{actual}'.");
    }
}
