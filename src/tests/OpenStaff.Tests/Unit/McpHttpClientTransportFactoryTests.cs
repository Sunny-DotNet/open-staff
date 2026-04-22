using System.Net;
using System.Net.Http.Headers;
using System.Text;
using OpenStaff.Mcp.Services;

namespace OpenStaff.Tests.Unit;

public sealed class McpHttpClientTransportFactoryTests
{
    [Fact]
    public async Task CreateHttpClient_StripsCharsetFromJsonRequestContentType()
    {
        MediaTypeHeaderValue? capturedContentType = null;
        using var client = McpHttpClientTransportFactory.CreateHttpClient(new StubHttpMessageHandler(request =>
        {
            capturedContentType = request.Content?.Headers.ContentType;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/mcp")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        await client.SendAsync(request);

        Assert.NotNull(capturedContentType);
        Assert.Equal("application/json", capturedContentType!.MediaType);
        Assert.True(string.IsNullOrWhiteSpace(capturedContentType.CharSet));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
