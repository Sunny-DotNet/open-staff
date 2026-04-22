using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenStaff.AgentSouls.Dtos;
using OpenStaff.AgentSouls.Services;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Skills.Models;

namespace OpenStaff.Tests.Unit;

public class AgentRoleTemplateImportServiceTests
{
    [Fact]
    public async Task PreviewAsync_ResolvesCapabilities_ByPackageAndSkillIdentity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        db.McpServers.AddRange(
            new McpServer
            {
                Id = Guid.NewGuid(),
                Name = "Workspace Filesystem",
                Source = McpSources.Custom,
                NpmPackage = "@modelcontextprotocol/server-filesystem",
                IsEnabled = true,
            },
            new McpServer
            {
                Id = Guid.NewGuid(),
                Name = "Python Fetch",
                Source = McpSources.Marketplace,
                PypiPackage = "mcp-server-fetch",
                IsEnabled = true,
            });
        await db.SaveChangesAsync();

        var service = CreateService(
            db,
            new FakeManagedSkillStore([
                CreateInstalledSkill("github", "awesome-copilot", "gh-cli"),
            ]));

        var preview = await service.PreviewAsync(
            """
            {
              "id": "44373B4E-397F-4C03-9C69-D3E95D59F87F",
              "name": "Monica",
              "jobTitle": "秘书",
              "description": "模板角色",
              "model": "gpt-5.4-mini",
              "modelConfig": "{\n  \"modelParameters\": {\n    \"temperature\": 0.7\n  }\n}",
              "soul": {
                "traits": ["直率", "高效"],
                "style": "技术流",
                "attitudes": ["细节优先"]
              },
              mcps: [
                { "mcpServerName": "Filesystem", "npmPackage": "@modelcontextprotocol/server-filesystem" },
                { "mcpServerName": "Fetch", "pypiPackage": "mcp-server-fetch" }
              ],
              skills: [
                { "source": "github/awesome-copilot", "skillId": "gh-cli" },
                { "source": "anthropics/skills", "skillId": "pdf" }
              ]
            }
            """,
            CancellationToken.None);

        Assert.Equal("Monica", preview.Role.Name);
        Assert.Equal("secretary", preview.Role.JobTitle);
        Assert.Equal("gpt-5.4-mini", preview.Role.ModelName);
        Assert.Equal(2, preview.Mcps.Count);
        Assert.Equal(2, preview.Skills.Count);

        var filesystem = Assert.Single(preview.Mcps, item => item.NpmPackage == "@modelcontextprotocol/server-filesystem");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Resolved, filesystem.Status);
        Assert.Equal("npmPackage", filesystem.MatchStrategy);
        Assert.Equal("Workspace Filesystem", filesystem.MatchedServerName);

        var fetch = Assert.Single(preview.Mcps, item => item.PypiPackage == "mcp-server-fetch");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Resolved, fetch.Status);
        Assert.Equal("pypiPackage", fetch.MatchStrategy);
        Assert.Equal("Python Fetch", fetch.MatchedServerName);

        var ghCli = Assert.Single(preview.Skills, item => item.SkillId == "gh-cli");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Resolved, ghCli.Status);
        Assert.Equal("github/awesome-copilot:gh-cli", ghCli.InstallKey);

        var pdf = Assert.Single(preview.Skills, item => item.SkillId == "pdf");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Missing, pdf.Status);
    }

    [Fact]
    public async Task ImportAsync_CreatesRoleAndRoleLevelBindings_FromTemplate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var filesystemServerId = Guid.NewGuid();
        db.McpServers.Add(new McpServer
        {
            Id = filesystemServerId,
            Name = "Filesystem",
            Source = McpSources.Custom,
            NpmPackage = "@modelcontextprotocol/server-filesystem",
            IsEnabled = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(
            db,
            new FakeManagedSkillStore([
                CreateInstalledSkill("github", "awesome-copilot", "gh-cli"),
            ]),
            new FakeAgentSoulService(
                personalityTraits: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("direct", new Dictionary<string, string>
                    {
                        ["en"] = "Direct",
                        ["zh"] = "直率"
                    }),
                    new AgentSoulValue("efficient", new Dictionary<string, string>
                    {
                        ["en"] = "Efficient",
                        ["zh"] = "高效"
                    })
                ]),
                communicationStyles: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("technical", new Dictionary<string, string>
                    {
                        ["en"] = "Technical",
                        ["zh"] = "技术流"
                    })
                ]),
                workAttitudes: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("detail_first", new Dictionary<string, string>
                    {
                        ["en"] = "Detail First",
                        ["zh"] = "细节优先"
                    })
                ])));

        var imported = await service.ImportAsync(
            """
            {
              "id": "44373B4E-397F-4C03-9C69-D3E95D59F87F",
              "name": "Monica",
              "jobTitle": "秘书",
              "description": "模板角色",
              "model": "gpt-5.4-mini",
              "modelConfig": "{\n  \"modelParameters\": {\n    \"temperature\": 0.7\n  }\n}",
              "soul": {
                "traits": ["直率", "高效"],
                "style": "技术流",
                "attitudes": ["细节优先"]
              },
              mcps: [
                { "mcpServerName": "Filesystem", "npmPackage": "@modelcontextprotocol/server-filesystem" }
              ],
              skills: [
                { "source": "github/awesome-copilot", "skillId": "gh-cli" }
              ]
            }
            """,
            overwriteExisting: true,
            CancellationToken.None);

        Assert.Equal("Monica", imported.Role.Name);
        Assert.Equal(1, imported.AddedMcpBindings);
        Assert.Equal(1, imported.AddedSkillBindings);

        var role = await db.AgentRoles.SingleAsync(item => item.Name == "Monica");
        Assert.Equal("secretary", role.JobTitle);
        Assert.Equal("gpt-5.4-mini", role.ModelName);
        Assert.Contains("\"temperature\": 0.7", role.Config);
        Assert.NotNull(role.Soul);
        Assert.Equal(["direct", "efficient"], role.Soul!.Traits);
        Assert.Equal("technical", role.Soul.Style);
        Assert.Equal(["detail_first"], role.Soul.Attitudes);

        var mcpBinding = await db.AgentRoleMcpBindings.SingleAsync(item => item.AgentRoleId == role.Id);
        Assert.Equal(filesystemServerId, mcpBinding.McpServerId);

        var skillBinding = await db.AgentRoleSkillBindings.SingleAsync(item => item.AgentRoleId == role.Id);
        Assert.Equal("github/awesome-copilot:gh-cli", skillBinding.SkillInstallKey);
        Assert.Equal("gh-cli", skillBinding.SkillId);
    }

    [Fact]
    public async Task PreviewAsync_SupportsRemoteRoleSyncV2Fields_AndKeyOnlyCapabilities()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        db.McpServers.AddRange(
            new McpServer
            {
                Id = Guid.NewGuid(),
                Name = "Filesystem",
                Source = McpSources.Marketplace,
                DefaultConfig = """
                {
                  "key": "filesystem"
                }
                """,
                NpmPackage = "@modelcontextprotocol/server-filesystem",
                IsEnabled = true,
            },
            new McpServer
            {
                Id = Guid.NewGuid(),
                Name = "GitHub",
                Source = McpSources.Marketplace,
                DefaultConfig = """
                {
                  "key": "github"
                }
                """,
                IsEnabled = true,
            });
        await db.SaveChangesAsync();

        var service = CreateService(
            db,
            new FakeManagedSkillStore([
                CreateInstalledSkill("anthropics", "skills", "pdf"),
                CreateInstalledSkill("github", "awesome-copilot", "gh-cli"),
            ]));

        var preview = await service.PreviewAsync(
            """
            {
              "schema": "openstaff.role-sync.v2",
              "id": "AC2E24E3-69D9-4112-9FF5-F59A2877A624",
              "name": "Monica",
              "job": "secretary",
              "description": "Talks with users and coordinates delivery.",
              "model": "glm-5.1",
              "soul": {
                "traits": ["loyal"],
                "style": "storyteller",
                "attitudes": ["collaborative"]
              },
              "mcps": [
                { "key": "filesystem" },
                { "key": "github" }
              ],
              "skills": [
                { "key": "anthropics/skills:pdf" },
                { "key": "github/awesome-copilot:gh-cli" }
              ]
            }
            """,
            CancellationToken.None);

        Assert.Equal("Monica", preview.Role.Name);
        Assert.Equal("secretary", preview.Role.JobTitle);

        var filesystem = Assert.Single(preview.Mcps, item => item.Key == "filesystem");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Resolved, filesystem.Status);
        Assert.Equal("key", filesystem.MatchStrategy);
        Assert.Equal("Filesystem", filesystem.MatchedServerName);

        var github = Assert.Single(preview.Mcps, item => item.Key == "github");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Resolved, github.Status);

        var pdf = Assert.Single(preview.Skills, item => item.Key == "anthropics/skills:pdf");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Resolved, pdf.Status);
        Assert.Equal("key", pdf.MatchStrategy);

        var ghCli = Assert.Single(preview.Skills, item => item.Key == "github/awesome-copilot:gh-cli");
        Assert.Equal(AgentRoleTemplateResolutionStatuses.Resolved, ghCli.Status);
    }

    private static AgentRoleTemplateImportService CreateService(
        AppDbContext db,
        IManagedSkillStore managedSkillStore,
        IAgentSoulService? agentSoulService = null)
    {
        return new AgentRoleTemplateImportService(
            new AgentRoleRepository(db),
            new McpServerRepository(db),
            new AgentRoleMcpBindingRepository(db),
            new AgentRoleSkillBindingRepository(db),
            managedSkillStore,
            db,
            agentSoulService);
    }

    private static ManagedInstalledSkill CreateInstalledSkill(string owner, string repo, string skillId)
        => new(
            Guid.NewGuid(),
            $"{owner}/{repo}:{skillId}",
            "github",
            $"{owner}/{repo}",
            owner,
            repo,
            skillId,
            skillId,
            skillId,
            $"https://github.com/{owner}/{repo}",
            1,
            $@"A:\skills\{owner}-{repo}-{skillId}",
            SkillInstallStatuses.Installed,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class FakeManagedSkillStore(IEnumerable<ManagedInstalledSkill>? installed = null) : IManagedSkillStore
    {
        private readonly List<ManagedInstalledSkill> _installed = installed?.ToList() ?? [];

        public Task<IReadOnlyList<ManagedInstalledSkill>> GetInstalledAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ManagedInstalledSkill>>(_installed.ToList());

        public Task<ManagedInstalledSkill?> GetByInstallKeyAsync(string installKey, CancellationToken ct = default)
            => Task.FromResult(_installed.FirstOrDefault(item =>
                string.Equals(item.InstallKey, installKey, StringComparison.OrdinalIgnoreCase)));

        public Task<ManagedInstalledSkill?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_installed.FirstOrDefault(item => item.Id == id));

        public Task<ManagedInstalledSkill> InstallAsync(SkillCatalogEntry catalogItem, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> RemoveAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SkillStoreMaintenanceResult> CheckForUpdatesAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SkillStoreMaintenanceResult> UpdateAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAgentSoulService : IAgentSoulService
    {
        public FakeAgentSoulService(
            IAgentSoulHttpService personalityTraits,
            IAgentSoulHttpService communicationStyles,
            IAgentSoulHttpService workAttitudes)
        {
            PersonalityTraits = personalityTraits;
            CommunicationStyles = communicationStyles;
            WorkAttitudes = workAttitudes;
        }

        public IAgentSoulHttpService CommunicationStyles { get; }

        public IAgentSoulHttpService PersonalityTraits { get; }

        public IAgentSoulHttpService WorkAttitudes { get; }
    }

    private sealed class FakeAgentSoulHttpService : IAgentSoulHttpService
    {
        private readonly IReadOnlyCollection<AgentSoulValue> _values;

        public FakeAgentSoulHttpService(IReadOnlyCollection<AgentSoulValue> values)
        {
            _values = values;
        }

        public string DefaultAliasName => "en";

        public Task<IReadOnlyCollection<AgentSoulValue>> GetAllAsync() => Task.FromResult(_values);

        public Task<IReadOnlyDictionary<string, string>> GetAllByLocaleAsync(string? locale = null)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<string> GetAsync(string key, string? locale = null)
            => throw new NotSupportedException();
    }
}
