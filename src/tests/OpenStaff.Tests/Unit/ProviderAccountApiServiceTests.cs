using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Application.Providers.Services;
using OpenStaff.Configurations;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Options;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;
using System.Text.Json;

namespace OpenStaff.Tests.Unit;

public class ProviderAccountApiServiceTests
{
    [Fact]
    public async Task GetAllProvidersAsync_ReturnsPlatformMetadataFromRegistry()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var workingDirectory = CreateWorkingDirectory();
        using var runtimeServices = CreateRuntimeServiceProvider(workingDirectory);
        var service = CreateApiService(
            db,
            workingDirectory,
            runtimeServices,
            out _,
            new PlatformRegistry([
                new TestPlatform(new TestProtocol(runtimeServices)),
                new TestPlatform(new SecondaryTestProtocol(runtimeServices))
            ]));

        try
        {
            var providers = await service.GetAllProvidersAsync();

            Assert.Collection(providers.OrderBy(provider => provider.Key),
                provider =>
                {
                    Assert.Equal("secondary-test-protocol", provider.Key);
                    Assert.Equal("Secondary Test Protocol", provider.DisplayName);
                    Assert.Equal("secondary-logo", provider.Logo);
                },
                provider =>
                {
                    Assert.Equal(TestProtocol.ProtocolKeyValue, provider.Key);
                    Assert.Equal("Test Protocol", provider.DisplayName);
                    Assert.Equal("test", provider.Logo);
                });
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public async Task GetAllAsync_AppliesProtocolAndEnabledFilters()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        db.ProviderAccounts.AddRange(
            new ProviderAccount
            {
                Id = Guid.NewGuid(),
                Name = "GitHub Enabled",
                ProtocolType = "github-copilot",
                IsEnabled = true
            },
            new ProviderAccount
            {
                Id = Guid.NewGuid(),
                Name = "GitHub Disabled",
                ProtocolType = "github-copilot",
                IsEnabled = false
            },
            new ProviderAccount
            {
                Id = Guid.NewGuid(),
                Name = "OpenAI Enabled",
                ProtocolType = "openai",
                IsEnabled = true
            });
        await db.SaveChangesAsync();

        var workingDirectory = CreateWorkingDirectory();
        using var runtimeServices = CreateRuntimeServiceProvider(workingDirectory);
        var service = CreateApiService(db, workingDirectory, runtimeServices, out _);

        try
        {
            var result = await service.GetAllAsync(new ProviderAccountQueryInput
            {
                ProtocolTypes = ["github-copilot"],
                IsEnabled = true
            });

            var item = Assert.Single(result.Items);
            Assert.Equal(1, result.Total);
            Assert.Equal("GitHub Enabled", item.Name);
            Assert.Equal("github-copilot", item.ProtocolType);
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public async Task UpdateAsync_DoesNotOverwriteConfiguration_WhenUpdatingMetadataOnly()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var workingDirectory = CreateWorkingDirectory();
        using var runtimeServices = CreateRuntimeServiceProvider(workingDirectory);
        var service = CreateApiService(db, workingDirectory, runtimeServices, out var providerAccountService);
        var created = await providerAccountService.CreateAsync(new CreateProviderAccountRequest
        {
            Name = "Original",
            ProtocolType = TestProtocol.ProtocolKeyValue,
            IsEnabled = true,
            EnvConfig = new Dictionary<string, object?>
            {
                ["ApiKey"] = "secret",
                ["BaseUrl"] = "https://old.example.com"
            }
        });

        try
        {
            var updated = await service.UpdateAsync(created.Id, new UpdateProviderAccountInput
            {
                Name = "Renamed"
            });

            var entity = await db.ProviderAccounts.FindAsync(created.Id);
            Assert.NotNull(entity);
            Assert.Equal("Renamed", updated.Name);
            Assert.Equal("Renamed", entity.Name);

            var config = await providerAccountService.GetEnvConfigAsync<TestProtocolEnv>(entity);
            Assert.NotNull(config);
            Assert.Equal("secret", config.ApiKey);
            Assert.Equal("https://old.example.com", config.BaseUrl);
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public async Task LoadConfigurationAsync_ReturnsEnvelopeAndOmitsEncryptedFields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var workingDirectory = CreateWorkingDirectory();
        using var runtimeServices = CreateRuntimeServiceProvider(workingDirectory);
        var service = CreateApiService(db, workingDirectory, runtimeServices, out var providerAccountService);
        var created = await providerAccountService.CreateAsync(new CreateProviderAccountRequest
        {
            Name = "Configured",
            ProtocolType = TestProtocol.ProtocolKeyValue,
            IsEnabled = true,
            EnvConfig = new Dictionary<string, object?>
            {
                ["ApiKey"] = "secret",
                ["BaseUrl"] = "https://example.com",
                ["UseOAuth"] = true
            }
        });

        try
        {
            var result = await service.LoadConfigurationAsync(created.Id);

            Assert.Collection(result.Properties,
                property =>
                {
                    Assert.Equal(JsonNamingPolicy.SnakeCaseLower.ConvertName(nameof(TestProtocolEnv.ApiKey)), property.Name);
                    Assert.Equal(ConfigurationPropertyType.String, property.Type);
                    Assert.True(property.Required);
                },
                property =>
                {
                    Assert.Equal(JsonNamingPolicy.SnakeCaseLower.ConvertName(nameof(TestProtocolEnv.BaseUrl)), property.Name);
                    Assert.Equal(ConfigurationPropertyType.String, property.Type);
                    Assert.True(property.Required);
                },
                property =>
                {
                    Assert.Equal(JsonNamingPolicy.SnakeCaseLower.ConvertName(nameof(TestProtocolEnv.UseOAuth)), property.Name);
                    Assert.Equal(ConfigurationPropertyType.Boolean, property.Type);
                    Assert.True(property.Required);
                });

            var apiKeyPropertyName = JsonNamingPolicy.SnakeCaseLower.ConvertName(nameof(TestProtocolEnv.ApiKey));
            var baseUrlPropertyName = JsonNamingPolicy.SnakeCaseLower.ConvertName(nameof(TestProtocolEnv.BaseUrl));
            var useOAuthPropertyName = JsonNamingPolicy.SnakeCaseLower.ConvertName(nameof(TestProtocolEnv.UseOAuth));

            Assert.Equal(JsonValueKind.Object, result.Configuration.ValueKind);
            Assert.False(result.Configuration.TryGetProperty(apiKeyPropertyName, out _));
            Assert.Equal("https://example.com", result.Configuration.GetProperty(baseUrlPropertyName).GetString());
            Assert.True(result.Configuration.GetProperty(useOAuthPropertyName).GetBoolean());
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public async Task SaveConfigurationAsync_PreservesMissingEncryptedFieldsAndUpdatesTimestamp()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var workingDirectory = CreateWorkingDirectory();
        using var runtimeServices = CreateRuntimeServiceProvider(workingDirectory);
        var service = CreateApiService(db, workingDirectory, runtimeServices, out var providerAccountService);
        var created = await providerAccountService.CreateAsync(new CreateProviderAccountRequest
        {
            Name = "Configured",
            ProtocolType = TestProtocol.ProtocolKeyValue,
            IsEnabled = true,
            EnvConfig = new Dictionary<string, object?>
            {
                ["ApiKey"] = "secret",
                ["BaseUrl"] = "https://old.example.com"
            }
        });

        try
        {
            using var document = JsonDocument.Parse("""{"base_url":"https://new.example.com"}""");
            await service.SaveConfigurationAsync(created.Id, document.RootElement);

            var entity = await db.ProviderAccounts.FindAsync(created.Id);
            Assert.NotNull(entity);
            Assert.NotNull(entity.UpdatedAt);

            var configuration = await providerAccountService.GetEnvConfigAsync<TestProtocolEnv>(entity);
            Assert.NotNull(configuration);
            Assert.Equal("secret", configuration.ApiKey);
            Assert.Equal("https://new.example.com", configuration.BaseUrl);
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    private static ProviderAccountApiService CreateApiService(
        AppDbContext db,
        string workingDirectory,
        ServiceProvider runtimeServices,
        out ProviderAccountService providerAccountService,
        IPlatformRegistry? platformRegistry = null)
    {
        var protocol = new TestProtocol(runtimeServices);
        var protocolFactory = CreateProtocolFactory(protocol);
        providerAccountService = CreateProviderAccountService(db, workingDirectory, protocolFactory.Object);
        var configurationService = new ProviderAccountConfigurationService(providerAccountService, protocolFactory.Object, db);

        return new ProviderAccountApiService(
            runtimeServices,
            new ProviderAccountRepository(db),
            db,
            providerAccountService,
            configurationService,
            platformRegistry ?? new PlatformRegistry(Array.Empty<IPlatform>()));
    }

    private static Mock<IProtocolFactory> CreateProtocolFactory(TestProtocol protocol)
    {
        var protocolFactory = new Mock<IProtocolFactory>();
        protocolFactory
            .Setup(factory => factory.GetProtocolEnvType(It.IsAny<string>()))
            .Returns((string protocolType) => string.Equals(protocolType, TestProtocol.ProtocolKeyValue, StringComparison.OrdinalIgnoreCase)
                ? typeof(TestProtocolEnv)
                : null);
        protocolFactory
            .Setup(factory => factory.AllProtocols())
            .Returns([protocol]);

        return protocolFactory;
    }

    private static ServiceProvider CreateRuntimeServiceProvider(string workingDirectory)
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment
            .SetupGet(environment => environment.EnvironmentName)
            .Returns(Environments.Development);

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(hostEnvironment.Object)
            .AddSingleton<IModelDataSource>(Mock.Of<IModelDataSource>())
            .AddSingleton(new EncryptionService("provider-account-api-tests"))
            .AddSingleton<IOptions<OpenStaffOptions>>(Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions
            {
                WorkingDirectory = workingDirectory
            }))
            .BuildServiceProvider();
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static ProviderAccountService CreateProviderAccountService(AppDbContext db, string workingDirectory, IProtocolFactory protocolFactory)
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment
            .SetupGet(environment => environment.EnvironmentName)
            .Returns(Environments.Development);

        return new ProviderAccountService(
            new ProviderAccountRepository(db),
            db,
            new EncryptionService("provider-account-api-tests"),
            protocolFactory,
            hostEnvironment.Object,
            Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions
            {
                WorkingDirectory = workingDirectory
            }));
    }

    private static string CreateWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "openstaff-provider-account-api-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        return workingDirectory;
    }

    private static void DeleteWorkingDirectory(string workingDirectory)
    {
        if (Directory.Exists(workingDirectory))
            Directory.Delete(workingDirectory, recursive: true);
    }

    private sealed class TestProtocol(IServiceProvider serviceProvider) : ProtocolBase<TestProtocolEnv>(serviceProvider)
    {
        public const string ProtocolKeyValue = "test-protocol";

        public override bool IsVendor => true;
        public override string ProtocolKey => ProtocolKeyValue;
        public override string ProtocolName => "Test Protocol";
        public override string Logo => "test";

        public override Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ModelInfo>>([]);
    }

    private sealed class SecondaryTestProtocol(IServiceProvider serviceProvider) : ProtocolBase<TestProtocolEnv>(serviceProvider)
    {
        public override bool IsVendor => true;
        public override string ProtocolKey => "secondary-test-protocol";
        public override string ProtocolName => "Secondary Test Protocol";
        public override string Logo => "secondary-logo";

        public override Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ModelInfo>>([]);
    }

    private sealed class TestPlatform(IProtocol protocol) : IPlatform, IHasProtocol
    {
        public string PlatformKey => protocol.ProtocolKey;

        public IProtocol GetProtocol() => protocol;
    }

    private sealed class TestProtocolEnv : ProtocolEnvBase
    {
        [Encrypted]
        public string ApiKey { get; set; } = string.Empty;

        public override string BaseUrl { get; set; } = string.Empty;

        public bool UseOAuth { get; set; }
    }
}

