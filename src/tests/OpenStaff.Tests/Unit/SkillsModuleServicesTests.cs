using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Options;
using OpenStaff.Skills.Models;
using OpenStaff.Skills.Services;
using OpenStaff.Skills.Sources;

namespace OpenStaff.Tests.Unit;

public sealed class SkillsModuleServicesTests : IDisposable
{
    private readonly string _workingDirectory;

    public SkillsModuleServicesTests()
    {
        _workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "openstaff-skills-module-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDirectory);
    }

    [Fact]
    public async Task SkillCatalogService_SearchAsync_AppliesKeywordFilterAndPaging()
    {
        var source = new FakeCatalogSource(
        [
            new SkillCatalogEntry
            {
                SourceKey = "skills.sh",
                Owner = "vercel",
                Repo = "skills",
                SkillId = "find-skills",
                Name = "find-skills",
                DisplayName = "Find Skills",
                Description = "Search the catalog",
                RepositoryUrl = "https://github.com/vercel/skills",
                Installs = 99
            },
            new SkillCatalogEntry
            {
                SourceKey = "skills.sh",
                Owner = "microsoft",
                Repo = "skills",
                SkillId = "maps",
                Name = "maps",
                DisplayName = "Maps",
                Description = "Map skills",
                RepositoryUrl = "https://github.com/microsoft/skills",
                Installs = 50
            },
            new SkillCatalogEntry
            {
                SourceKey = "skills.sh",
                Owner = "microsoft",
                Repo = "skills",
                SkillId = "mail",
                Name = "mail",
                DisplayName = "Mail",
                Description = "Mail skills",
                RepositoryUrl = "https://github.com/microsoft/skills",
                Installs = 10
            }
        ]);
        var service = new SkillCatalogService(source);

        var result = await service.SearchAsync(new SkillCatalogQuery
        {
            Keyword = "m",
            Owner = "microsoft",
            Page = 1,
            PageSize = 1
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("maps", item.SkillId);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task SkillsShCatalogSource_GetAsync_EnrichesDescriptionFromSkillDocument()
    {
        var snapshotJson = """
        {
          "skills": [
            {
              "owner": "vercel",
              "repo": "skills",
              "skillId": "find-skills",
              "name": "find-skills",
              "displayName": "Find Skills",
              "githubUrl": "https://github.com/vercel/skills",
              "installs": 42
            }
          ]
        }
        """;
        var zipBytes = CreateSkillArchive(
            "repo-root",
            "find-skills",
            """
            ---
            name: Find Skills
            description: Search and inspect available skills.
            ---
            # Find Skills
            """);
        var factory = new StubHttpClientFactory(name => name switch
        {
            "OpenStaff.Skills.Catalog" => CreateJsonClient(snapshotJson),
            "OpenStaff.Skills.GitHub" => CreateBinaryClient(zipBytes),
            _ => throw new InvalidOperationException($"Unexpected client '{name}'.")
        });
        var archiveClient = new GitHubSkillArchiveClient(factory);
        var source = new SkillsShCatalogSource(factory, archiveClient, NullLogger<SkillsShCatalogSource>.Instance);

        var item = await source.GetAsync("vercel", "skills", "find-skills");

        var resolved = Assert.IsType<SkillCatalogEntry>(item);
        Assert.Equal("Find Skills", resolved.DisplayName);
        Assert.Equal("Search and inspect available skills.", resolved.Description);
        Assert.Equal("https://github.com/vercel/skills", resolved.RepositoryUrl);
    }

    [Fact]
    public async Task SkillsShCatalogSource_GetAsync_FallsBackToSnapshot_WhenArchiveDownloadFails()
    {
        var snapshotJson = """
        {
          "skills": [
            {
              "owner": "vercel",
              "repo": "skills",
              "skillId": "find-skills",
              "name": "find-skills",
              "displayName": "Find Skills",
              "githubUrl": "https://github.com/vercel/skills",
              "installs": 42
            }
          ]
        }
        """;
        var factory = new StubHttpClientFactory(name => name switch
        {
            "OpenStaff.Skills.Catalog" => CreateJsonClient(snapshotJson),
            "OpenStaff.Skills.GitHub" => new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("rate limit exceeded", Encoding.UTF8, "text/plain")
            }))
            {
                BaseAddress = new Uri("https://api.github.com/")
            },
            _ => throw new InvalidOperationException($"Unexpected client '{name}'.")
        });
        var archiveClient = new GitHubSkillArchiveClient(factory);
        var source = new SkillsShCatalogSource(factory, archiveClient, NullLogger<SkillsShCatalogSource>.Instance);

        var item = await source.GetAsync("vercel", "skills", "find-skills");

        var resolved = Assert.IsType<SkillCatalogEntry>(item);
        Assert.Equal("Find Skills", resolved.DisplayName);
        Assert.Null(resolved.Description);
        Assert.Equal("https://github.com/vercel/skills", resolved.RepositoryUrl);
    }

    [Fact]
    public async Task ManagedSkillStore_InstallAsync_ReplacesExistingDirectoryAndPreservesInstallTime()
    {
        var service = CreateManagedStore(
            new Dictionary<string, byte[]>
            {
                ["sha-1"] = CreateSkillArchive(
                    "repo-root",
                    "find-skills",
                    """
                    ---
                    name: Find Skills
                    description: First version.
                    ---
                    """,
                    ("content.txt", "v1")),
                ["sha-2"] = CreateSkillArchive(
                    "repo-root",
                    "find-skills",
                    """
                    ---
                    name: Find Skills
                    description: Second version.
                    ---
                    """,
                    ("content.txt", "v2"))
            },
            ["sha-1", "sha-2"]);
        var entry = new SkillCatalogEntry
        {
            SourceKey = "skills.sh",
            Owner = "vercel",
            Repo = "skills",
            SkillId = "find-skills",
            Name = "find-skills",
            DisplayName = "Find Skills",
            RepositoryUrl = "https://github.com/vercel/skills",
            Installs = 42
        };

        var first = await service.InstallAsync(entry);
        var second = await service.InstallAsync(entry);
        var installed = Assert.Single(await service.GetInstalledAsync());

        Assert.Equal(first.CreatedAt, second.CreatedAt);
        Assert.True(second.UpdatedAt >= first.UpdatedAt);
        Assert.Equal("sha-2", installed.SourceRevision);
        Assert.Equal("v2", await File.ReadAllTextAsync(Path.Combine(installed.InstallRootPath, "content.txt")));
    }

    [Fact]
    public async Task ManagedSkillStore_RemoveAsync_RemovesInstalledDirectory()
    {
        var service = CreateManagedStore(
            new Dictionary<string, byte[]>
            {
                ["sha-1"] = CreateSkillArchive(
                    "repo-root",
                    "maps",
                    """
                    ---
                    name: Maps
                    description: Map skill.
                    ---
                    """,
                    ("content.txt", "maps"))
            },
            ["sha-1"]);
        var entry = new SkillCatalogEntry
        {
            SourceKey = "skills.sh",
            Owner = "microsoft",
            Repo = "skills",
            SkillId = "maps",
            Name = "maps",
            DisplayName = "Maps",
            RepositoryUrl = "https://github.com/microsoft/skills",
            Installs = 10
        };

        var installed = await service.InstallAsync(entry);
        var removed = await service.RemoveAsync(installed.Id);

        Assert.True(removed);
        Assert.False(Directory.Exists(installed.InstallRootPath));
        Assert.Empty(await service.GetInstalledAsync());
    }

    [Fact]
    public async Task ManagedSkillStore_InstallAsync_FallsBackToGitHeadResolver_WhenGitHubCommitApiIsRateLimited()
    {
        var archive = CreateSkillArchive(
            "repo-root",
            "canvas-design",
            """
            ---
            name: canvas-design
            description: Canvas design skill.
            ---
            """,
            ("content.txt", "canvas"));

        var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/commits", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("rate limit exceeded", Encoding.UTF8, "text/plain")
                };
            }

            if (path.EndsWith("/sha-fallback", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(archive)
                };
            }

            throw new InvalidOperationException($"Unexpected request '{path}'.");
        }))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var service = new ManagedSkillStore(
            new StubHttpClientFactory(_ => client),
            Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions
            {
                WorkingDirectory = _workingDirectory
            }),
            NullLogger<ManagedSkillStore>.Instance,
            (_, _, _) => Task.FromResult("sha-fallback"));

        var installed = await service.InstallAsync(new SkillCatalogEntry
        {
            SourceKey = "skills.sh",
            Owner = "anthropics",
            Repo = "skills",
            SkillId = "canvas-design",
            Name = "canvas-design",
            DisplayName = "Canvas Design",
            RepositoryUrl = "https://github.com/anthropics/skills",
            Installs = 1
        });

        Assert.Equal("sha-fallback", installed.SourceRevision);
        Assert.Equal("canvas", await File.ReadAllTextAsync(Path.Combine(installed.InstallRootPath, "content.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
            Directory.Delete(_workingDirectory, recursive: true);
    }

    private ManagedSkillStore CreateManagedStore(
        IReadOnlyDictionary<string, byte[]> archivesBySha,
        IReadOnlyList<string> commitSequence,
        Func<string, string, CancellationToken, Task<string>>? gitHeadResolver = null)
    {
        var client = CreateManagedGitHubClient(archivesBySha, commitSequence);
        return new ManagedSkillStore(
            new StubHttpClientFactory(_ => client),
            Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions
            {
                WorkingDirectory = _workingDirectory
            }),
            NullLogger<ManagedSkillStore>.Instance,
            gitHeadResolver);
    }

    private static HttpClient CreateJsonClient(string json)
        => new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));

    private static HttpClient CreateBinaryClient(byte[] payload)
    {
        var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
        }))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        return client;
    }

    private static HttpClient CreateManagedGitHubClient(
        IReadOnlyDictionary<string, byte[]> archivesBySha,
        IReadOnlyList<string> commitSequence)
    {
        var remainingCommits = new Queue<string>(commitSequence);
        return new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/commits", StringComparison.OrdinalIgnoreCase))
            {
                var sha = remainingCommits.Dequeue();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"[{{\"sha\":\"{sha}\"}}]", Encoding.UTF8, "application/json")
                };
            }

            var shaFromPath = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
            if (!archivesBySha.TryGetValue(shaFromPath, out var payload))
                throw new InvalidOperationException($"Unexpected archive request '{path}'.");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
        }))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
    }

    private static byte[] CreateSkillArchive(
        string rootDirectoryName,
        string skillDirectoryName,
        string skillDocument,
        params (string RelativePath, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var skillDocumentEntry = archive.CreateEntry($"{rootDirectoryName}/{skillDirectoryName}/SKILL.md");
            using (var writer = new StreamWriter(skillDocumentEntry.Open(), Encoding.UTF8, 1024, leaveOpen: false))
            {
                writer.Write(skillDocument);
            }

            foreach (var file in files)
            {
                var entry = archive.CreateEntry($"{rootDirectoryName}/{skillDirectoryName}/{file.RelativePath}");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, 1024, leaveOpen: false);
                writer.Write(file.Content);
            }
        }

        return stream.ToArray();
    }

    private sealed class FakeCatalogSource : ISkillCatalogSource
    {
        private readonly IReadOnlyList<SkillCatalogEntry> _items;

        public FakeCatalogSource(IReadOnlyList<SkillCatalogEntry> items)
        {
            _items = items;
        }

        public string SourceKey => "skills.sh";

        public string DisplayName => "skills.sh";

        public Task<IReadOnlyList<SkillCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_items);

        public Task<SkillCatalogEntry?> GetAsync(string owner, string repo, string skillId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item =>
                string.Equals(item.Owner, owner, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Repo, repo, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.SkillId, skillId, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<string, HttpClient> _clientFactory;

        public StubHttpClientFactory(Func<string, HttpClient> clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public HttpClient CreateClient(string name) => _clientFactory(name);
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
