using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace OpenStaff.Mcp.Services;

public static class McpHttpClientTransportFactory
{
    public static HttpClientTransport Create(HttpClientTransportOptions options, ILoggerFactory? loggerFactory = null)
    {
        var httpClient = CreateHttpClient();
        return new HttpClientTransport(options, httpClient, loggerFactory, ownsHttpClient: true);
    }

    internal static HttpClient CreateHttpClient(HttpMessageHandler? innerHandler = null)
    {
        var handler = innerHandler is null
            ? new StrictJsonContentTypeHandler()
            : new StrictJsonContentTypeHandler(innerHandler);
        return new HttpClient(handler, disposeHandler: true);
    }

    internal static void NormalizeJsonContentType(MediaTypeHeaderValue? contentType)
    {
        if (contentType is null)
            return;

        if (!string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(contentType.CharSet))
            return;

        contentType.CharSet = null;
    }

    internal sealed class StrictJsonContentTypeHandler : DelegatingHandler
    {
        public StrictJsonContentTypeHandler()
            : base(new HttpClientHandler())
        {
        }

        public StrictJsonContentTypeHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            NormalizeJsonContentType(request.Content?.Headers.ContentType);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
