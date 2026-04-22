using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Agent;
using OpenStaff.ApiServices;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Dtos;
using OpenStaff.Configurations;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;

namespace OpenStaff.Tests.Unit;

public sealed class AgentRoleApiServiceVendorModelCatalogTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public AgentRoleApiServiceVendorModelCatalogTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetVendorModelCatalogAsync_ShouldMapRequiresProviderConfigurationStatus()
    {
        var platform = new FakeVendorPlatform(
            "anthropic",
            "Anthropic Claude",
            VendorModelCatalogResult.RequiresProviderConfiguration("请先填写 API Key", "ApiKey"));
        var service = new AgentRoleApiService(new AgentRoleRepository(_db), _db, null!, new FakeVendorPlatformCatalog(platform), null!, null!);

        var catalog = await service.GetVendorModelCatalogAsync("anthropic");

        Assert.NotNull(catalog);
        Assert.Equal("anthropic", catalog!.ProviderType);
        Assert.Equal("requires_provider_configuration", catalog.Status);
        Assert.Contains("ApiKey", catalog.MissingConfigurationFields);
        Assert.Empty(catalog.Models);
    }

    [Fact]
    public async Task GetVendorModelsAsync_ShouldRemainBackwardCompatible_WithReadyCatalog()
    {
        var platform = new FakeVendorPlatform(
            "github-copilot",
            "GitHub Copilot",
            VendorModelCatalogResult.Ready(
            [
                new VendorModel("gpt-4o", "GPT-4o", "GPT-4o"),
                new VendorModel("claude-sonnet-4-20250514", "Claude Sonnet 4", "Claude 4"),
            ]));
        var service = new AgentRoleApiService(new AgentRoleRepository(_db), _db, null!, new FakeVendorPlatformCatalog(platform), null!, null!);

        var models = await service.GetVendorModelsAsync("github-copilot");

        Assert.Collection(models,
            model =>
            {
                Assert.Equal("gpt-4o", model.Id);
                Assert.Equal("GPT-4o", model.Name);
            },
            model =>
            {
                Assert.Equal("claude-sonnet-4-20250514", model.Id);
                Assert.Equal("Claude Sonnet 4", model.Name);
            });
    }

    [Fact]
    public async Task GetAllAsync_ShouldAppendVirtualVendorRoles_FromVendorPlatformCatalog()
    {
        var platform = new FakeVendorPlatform(
            "anthropic",
            "Anthropic Claude",
            VendorModelCatalogResult.Ready([]),
            avatarDataUri: "https://example.com/anthropic.png");
        var service = new AgentRoleApiService(new AgentRoleRepository(_db), _db, null!, new FakeVendorPlatformCatalog(platform), null!, null!);

        var roles = await service.GetAllAsync(CancellationToken.None);

        var role = Assert.Single(roles);
        Assert.Equal(Guid.Empty, role.Id);
        Assert.Equal("Anthropic Claude", role.Name);
        Assert.Equal("anthropic", role.RoleType);
        Assert.Equal("anthropic", role.ProviderType);
        Assert.Equal(AgentSource.Vendor, role.Source);
        Assert.Equal("https://example.com/anthropic.png", role.Avatar);
        Assert.True(role.IsVirtual);
        Assert.False(role.IsBuiltin);
    }

    [Fact]
    public async Task GetVendorProviderConfigurationAsync_ShouldMapVendorConfiguration_FromCatalog()
    {
        var configuration = new FakeVendorConfigurationService(
            [
                new ConfigurationProperty("Streaming", ConfigurationPropertyType.Boolean, true, false),
                new ConfigurationProperty("ApiKey", ConfigurationPropertyType.String, null, true)
            ],
            new Dictionary<string, object?>
            {
                ["Streaming"] = false,
                ["ApiKey"] = "secret"
            });
        var platform = new FakeVendorPlatform(
            "github-copilot",
            "GitHub Copilot",
            VendorModelCatalogResult.Ready([]),
            avatarDataUri: "https://example.com/copilot.png",
            configuration: configuration);
        var service = new AgentRoleApiService(new AgentRoleRepository(_db), _db, null!, new FakeVendorPlatformCatalog(platform), null!, null!);

        var result = await service.GetVendorProviderConfigurationAsync("github-copilot");

        Assert.NotNull(result);
        Assert.Equal("github-copilot", result!.ProviderType);
        Assert.Equal("GitHub Copilot", result.DisplayName);
        Assert.Equal("https://example.com/copilot.png", result.AvatarDataUri);
        Assert.Collection(
            result.Properties,
            property =>
            {
                Assert.Equal("Streaming", property.Name);
                Assert.Equal("boolean", property.FieldType);
                Assert.Equal(true, property.DefaultValue);
                Assert.False(property.Required);
            },
            property =>
            {
                Assert.Equal("ApiKey", property.Name);
                Assert.Equal("string", property.FieldType);
                Assert.Null(property.DefaultValue);
                Assert.True(property.Required);
            });
        Assert.Equal(false, result.Configuration["Streaming"]);
        Assert.Equal("secret", result.Configuration["ApiKey"]);
    }

    [Fact]
    public async Task UpdateVendorProviderConfigurationAsync_ShouldPersistViaVendorConfigurationService()
    {
        var configuration = new FakeVendorConfigurationService(
            [
                new ConfigurationProperty("Streaming", ConfigurationPropertyType.Boolean, true, false),
                new ConfigurationProperty("AutoApproved", ConfigurationPropertyType.Boolean, false, false)
            ],
            new Dictionary<string, object?>
            {
                ["Streaming"] = true
            });
        var platform = new FakeVendorPlatform(
            "github-copilot",
            "GitHub Copilot",
            VendorModelCatalogResult.Ready([]),
            configuration: configuration);
        var service = new AgentRoleApiService(new AgentRoleRepository(_db), _db, null!, new FakeVendorPlatformCatalog(platform), null!, null!);

        var result = await service.UpdateVendorProviderConfigurationAsync(
            "github-copilot",
            new UpdateVendorProviderConfigurationInput
            {
                Configuration = new Dictionary<string, object?>
                {
                    ["Streaming"] = false,
                    ["AutoApproved"] = true
                }
            });

        Assert.NotNull(result);
        Assert.Equal(false, configuration.CurrentValues["Streaming"]);
        Assert.Equal(true, configuration.CurrentValues["AutoApproved"]);
        Assert.Equal(false, result!.Configuration["Streaming"]);
        Assert.Equal(true, result.Configuration["AutoApproved"]);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeVendorPlatform
    {
        public FakeVendorPlatform(
            string providerType,
            string displayName,
            VendorModelCatalogResult catalog,
            string? avatarDataUri = null,
            FakeVendorConfigurationService? configuration = null)
        {
            Metadata = new FakeVendorPlatformMetadata(providerType, displayName, avatarDataUri);
            ModelCatalog = new FakeVendorModelCatalogService(catalog);
            Configuration = configuration;
        }

        public FakeVendorPlatformMetadata Metadata { get; }
        public FakeVendorModelCatalogService ModelCatalog { get; }
        public FakeVendorConfigurationService? Configuration { get; }
    }

    private sealed class FakeVendorPlatformMetadata(string providerType, string displayName, string? avatarDataUri = null) : IVendorPlatformMetadata
    {
        public string ProviderType { get; } = providerType;
        public string DisplayName { get; } = displayName;
        public string? AvatarDataUri { get; } = avatarDataUri;
    }

    private sealed class FakeVendorModelCatalogService(VendorModelCatalogResult catalog) : IVendorModelCatalogService
    {
        public Task<VendorModelCatalogResult> GetModelCatalogAsync(CancellationToken ct = default)
            => Task.FromResult(catalog);
    }

    private sealed class FakeVendorConfigurationService(
        ConfigurationProperty[] properties,
        Dictionary<string, object?> currentValues) : IVendorConfigurationService
    {
        public ConfigurationProperty[] ConfigurationProperties { get; } = properties;

        public Dictionary<string, object?> CurrentValues { get; private set; } =
            new(currentValues, StringComparer.OrdinalIgnoreCase);

        public Task<Dictionary<string, object?>> GetConfigurationValuesAsync(CancellationToken ct = default)
            => Task.FromResult(new Dictionary<string, object?>(CurrentValues, StringComparer.OrdinalIgnoreCase));

        public Task SetConfigurationValuesAsync(Dictionary<string, object?> configuration, CancellationToken ct = default)
        {
            CurrentValues = new Dictionary<string, object?>(configuration, StringComparer.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVendorPlatformCatalog : IVendorPlatformCatalog
    {
        private readonly Dictionary<string, VendorPlatformServices> _platforms;

        public FakeVendorPlatformCatalog(params FakeVendorPlatform[] platforms)
        {
            _platforms = platforms.ToDictionary(
                platform => platform.Metadata.ProviderType,
                platform => new VendorPlatformServices(platform.Metadata, platform.ModelCatalog, platform.Configuration),
                StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, VendorPlatformServices> Platforms => _platforms;

        public bool TryGetVendorPlatform(string providerType, out VendorPlatformServices platform)
            => _platforms.TryGetValue(providerType, out platform!);
    }
}

