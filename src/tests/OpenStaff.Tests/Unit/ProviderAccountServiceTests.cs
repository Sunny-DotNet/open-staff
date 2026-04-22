using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OpenStaff.Application.Providers.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Options;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Tests.Unit;

public class ProviderAccountServiceTests
{
    [Fact]
    public async Task CreateAsync_WritesEnvConfigToProviderFile()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        var workingDirectory = CreateWorkingDirectory();

        try
        {
            var service = CreateProviderAccountService(db, workingDirectory);

            var created = await service.CreateAsync(new CreateProviderAccountRequest
            {
                Name = "OpenAI",
                ProtocolType = "openai",
                IsEnabled = true,
                EnvConfig = new Dictionary<string, object>
                {
                    ["ApiKey"] = "secret",
                    ["BaseUrl"] = "https://example.com"
                }
            });

            var filePath = GetEnvConfigFilePath(workingDirectory, created.Id);
            Assert.True(File.Exists(filePath));
            Assert.Equal("""{"ApiKey":"secret","BaseUrl":"https://example.com"}""", await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesEnvConfigFile()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        var workingDirectory = CreateWorkingDirectory();

        try
        {
            var service = CreateProviderAccountService(db, workingDirectory);
            var created = await service.CreateAsync(new CreateProviderAccountRequest
            {
                Name = "OpenAI",
                ProtocolType = "openai",
                EnvConfig = new Dictionary<string, object>
                {
                    ["ApiKey"] = "secret"
                }
            });

            var filePath = GetEnvConfigFilePath(workingDirectory, created.Id);
            Assert.True(File.Exists(filePath));

            var deleted = await service.DeleteAsync(created.Id);

            Assert.True(deleted);
            Assert.False(File.Exists(filePath));
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public async Task GetEnvConfigDictAsync_ReadsFromProviderFile()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        var workingDirectory = CreateWorkingDirectory();

        try
        {
            var service = CreateProviderAccountService(db, workingDirectory);
            var account = await service.CreateAsync(new CreateProviderAccountRequest
            {
                Name = "GitHub Copilot",
                ProtocolType = "github-copilot",
                EnvConfig = new Dictionary<string, object>
                {
                    ["OAuthToken"] = "oauth-token"
                }
            });

            var envConfig = await service.GetEnvConfigDictAsync(account);

            Assert.NotNull(envConfig);
            Assert.Equal("oauth-token", envConfig["OAuthToken"]?.ToString());
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    private static ProviderAccountService CreateProviderAccountService(AppDbContext db, string workingDirectory)
    {
        var protocolFactory = new Mock<IProtocolFactory>();
        protocolFactory
            .Setup(factory => factory.GetProtocolEnvType(It.IsAny<string>()))
            .Returns((Type?)null);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment
            .SetupGet(environment => environment.EnvironmentName)
            .Returns(Environments.Development);

        return new ProviderAccountService(
            new ProviderAccountRepository(db),
            db,
            new EncryptionService("provider-account-service-tests"),
            protocolFactory.Object,
            hostEnvironment.Object,
            Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions
            {
                WorkingDirectory = workingDirectory
            }));
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

    private static string CreateWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "openstaff-provider-account-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        return workingDirectory;
    }

    private static string GetEnvConfigFilePath(string workingDirectory, Guid providerAccountId)
        => Path.Combine(workingDirectory, "providers", $"{providerAccountId}.json");

    private static void DeleteWorkingDirectory(string workingDirectory)
    {
        if (Directory.Exists(workingDirectory))
            Directory.Delete(workingDirectory, recursive: true);
    }
}

