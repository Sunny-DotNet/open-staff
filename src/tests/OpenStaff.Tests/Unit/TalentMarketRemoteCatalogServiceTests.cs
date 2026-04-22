using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.TalentMarket.Services;

namespace OpenStaff.Tests.Unit;

public class TalentMarketRemoteCatalogServiceTests
{
    [Fact]
    public async Task GetTemplatesAsync_ShouldLoadIndexAndTemplateById()
    {
        var service = CreateService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/index.json"] = """
            {
              "schema": "openstaff.template-index.v1",
              "templateCount": 2,
              "templates": [
                {
                  "file": "monica.role.json",
                  "id": "AC2E24E3-69D9-4112-9FF5-F59A2877A624",
                  "name": "Monica",
                    "job": "secretary",
                    "jobTitle": "Secretary",
                    "description": "Coordinates delivery.",
                    "avatar": "https://example.com/monica.png",
                    "model": "glm-5.1",
                    "source": "builtin",
                    "isBuiltin": true,
                  "isActive": true,
                  "mcpCount": 4,
                  "skillCount": 8
                },
                {
                  "file": "sophie.role.json",
                  "id": "4810B17F-6635-4C19-920A-7B80D78C5151",
                  "name": "Sophie",
                  "job": "backend_engineer",
                  "jobTitle": "Backend Engineer",
                  "description": "Builds APIs.",
                  "model": "gpt-5.4",
                  "source": "custom",
                  "isBuiltin": false,
                  "isActive": true,
                  "mcpCount": 2,
                  "skillCount": 1
                }
              ]
            }
            """,
            ["/templates/monica.role.json"] = """
            {
              "id": "AC2E24E3-69D9-4112-9FF5-F59A2877A624",
              "name": "Monica",
              "job": "secretary"
            }
            """
        });

        var templates = await service.GetTemplatesAsync();
        var monica = Assert.Single(templates, item => item.TemplateId == "AC2E24E3-69D9-4112-9FF5-F59A2877A624");

        Assert.Equal("Secretary", monica.JobTitle);
        Assert.Equal("https://example.com/monica.png", monica.Avatar);
        Assert.Equal(4, monica.McpCount);

        var template = await service.GetTemplateAsync(monica.TemplateId);

        Assert.NotNull(template);
        Assert.Equal("monica.role.json", template!.Summary.File);
        Assert.Contains("\"job\": \"secretary\"", template.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTemplateAsync_ShouldReturnNullForUnknownTemplate()
    {
        var service = CreateService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/index.json"] = """
            {
              "schema": "openstaff.template-index.v1",
              "templateCount": 0,
              "templates": []
            }
            """
        });

        var result = await service.GetTemplateAsync("missing");

        Assert.Null(result);
    }

    private static TalentMarketRemoteCatalogService CreateService(IReadOnlyDictionary<string, string> responses)
    {
        return new TalentMarketRemoteCatalogService(
            new StubHttpClientFactory(new StubHttpMessageHandler(request =>
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                if (!responses.TryGetValue(path, out var content)
                    && !responses.TryGetValue(path.Replace("/Sunny-DotNet/agents/main", string.Empty, StringComparison.OrdinalIgnoreCase), out content))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };
            })),
            NullLogger<TalentMarketRemoteCatalogService>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://raw.githubusercontent.com/Sunny-DotNet/agents/main/")
            };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responseFactory(request));
    }
}
