using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenStaff.Mcp;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Sources;

namespace OpenStaff.Tests.Unit;

public class StaticTemplateCatalogSourceTests
{
    [Fact]
    public async Task SearchAsync_ShouldResolveCurrentPagesTemplates()
    {
        var source = CreateSource(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/index.json"] = """
            [
              {
                "key": "filesystem",
                "display_name": "Filesystem",
                "description": "Filesystem summary",
                "category": "filesystem",
                "homepage": "https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem"
              },
              {
                "key": "github",
                "display_name": "GitHub",
                "description": "GitHub API 操作",
                "category": "dev-tools",
                "homepage": "https://github.com/modelcontextprotocol/servers/tree/main/src/github"
              },
              {
                "key": "github",
                "display_name": "GitHub",
                "description": "Official GitHub MCP deployment options",
                "category": "dev-tools",
                "homepage": "https://github.com/github/github-mcp-server"
              }
            ]
            """,
            ["/templates/filesystem.mcp.json"] = """
            {
              "template_id": "builtin.filesystem.legacy",
              "key": "filesystem",
              "display_name": "Filesystem",
              "description": "Filesystem detail",
              "category": "filesystem",
              "homepage": "https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem",
              "profiles": [
                {
                  "id": "package-npm",
                  "profile_type": "package",
                  "transport_type": "stdio",
                  "runner_kind": "package",
                  "runner": "npx",
                  "ecosystem": "npm",
                  "package_name": "@modelcontextprotocol/server-filesystem",
                  "package_version": "latest"
                }
              ],
              "parameter_schema": []
            }
            """,
            ["/templates/github-legacy.mcp.json"] = """
            {
              "template_id": "builtin.github.legacy",
              "key": "github",
              "display_name": "GitHub",
              "description": "Legacy GitHub MCP",
              "category": "dev-tools",
              "homepage": "https://github.com/modelcontextprotocol/servers/tree/main/src/github",
              "profiles": [
                {
                  "id": "package-npm",
                  "profile_type": "package",
                  "transport_type": "stdio",
                  "runner_kind": "package",
                  "runner": "npx",
                  "ecosystem": "npm",
                  "package_name": "@modelcontextprotocol/server-github",
                  "package_version": "latest"
                }
              ],
              "parameter_schema": []
            }
            """,
            ["/templates/github-official.mcp.json"] = """
            {
              "template_id": "official.github.current",
              "key": "github",
              "display_name": "GitHub",
              "description": "Official GitHub MCP",
              "category": "dev-tools",
              "homepage": "https://github.com/github/github-mcp-server",
              "profiles": [
                {
                  "id": "remote",
                  "profile_type": "remote",
                  "transport_type": "http",
                  "runner_kind": "remote",
                  "runner": "remote",
                  "url_template": "${param:remoteUrl}",
                  "headers_template": {
                    "Authorization": "Bearer ${param:accessToken}"
                  }
                }
              ],
              "parameter_schema": [
                {
                  "key": "remoteUrl",
                  "type": "string",
                  "required": true,
                  "default_value": "https://api.githubcopilot.com/mcp/",
                  "applies_to_profiles": ["remote"]
                },
                {
                  "key": "accessToken",
                  "type": "password",
                  "required": true,
                  "default_value": "",
                  "applies_to_profiles": ["remote"]
                }
              ]
            }
            """
        });

        var entries = await source.SearchAsync(new CatalogSearchQuery());

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, entry => entry.EntryId == "builtin.filesystem.legacy");
        Assert.Contains(entries, entry => entry.EntryId == "builtin.github.legacy");
        Assert.Contains(entries, entry => entry.EntryId == "official.github.current");

        var filesystem = Assert.Single(entries, entry => entry.EntryId == "builtin.filesystem.legacy");
        var filesystemChannel = Assert.Single(filesystem.InstallChannels);
        Assert.Equal(McpChannelType.Npm, filesystemChannel.ChannelType);
        Assert.Equal("@modelcontextprotocol/server-filesystem", filesystemChannel.PackageIdentifier);

        var githubOfficial = Assert.Single(entries, entry => entry.EntryId == "official.github.current");
        Assert.Contains(McpTransportType.Http, githubOfficial.TransportTypes);
        var remoteChannel = Assert.Single(githubOfficial.InstallChannels);
        Assert.Equal(McpChannelType.Remote, remoteChannel.ChannelType);
        Assert.Equal("https://api.githubcopilot.com/mcp/", remoteChannel.ArtifactUrl);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullForUnknownTemplate()
    {
        var source = CreateSource(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/index.json"] = """[]"""
        });

        var entry = await source.GetByIdAsync("missing");

        Assert.Null(entry);
    }

    private static StaticTemplateCatalogSource CreateSource(IReadOnlyDictionary<string, string> responses)
    {
        return new StaticTemplateCatalogSource(
            new StubHttpClientFactory(new StubHttpMessageHandler(request =>
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                if (!responses.TryGetValue(path, out var content))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };
            })),
            Microsoft.Extensions.Options.Options.Create(new OpenStaffMcpOptions
            {
                TemplateCatalogBaseUrl = "https://mcps.example"
            }),
            NullLogger<StaticTemplateCatalogSource>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false);
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
