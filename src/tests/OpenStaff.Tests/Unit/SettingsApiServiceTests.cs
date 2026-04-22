using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.ApiServices;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.Dtos;
using OpenStaff.Application.Settings.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class SettingsApiServiceTests
{
    /// <summary>
    /// zh-CN: 验证系统设置读取时会把团队描述和 ProjectGroup 自动审批开关从持久化配置映射出来。
    /// en: Verifies system settings retrieval maps the team description and ProjectGroup auto-approval toggle from persisted settings.
    /// </summary>
    [Fact]
    public async Task GetSystemAsync_ReadsTeamDescriptionAndAutoApprovalFlag()
    {
        using var context = new TestContext();
        context.Db.GlobalSettings.AddRange(
            new GlobalSetting
            {
                Key = SystemSettingsKeys.TeamDescription,
                Value = "负责协调多个智能体与用户需求。"
            },
            new GlobalSetting
            {
                Key = SystemSettingsKeys.ProjectGroupAutoApproveCapabilities,
                Value = "true"
            });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetSystemAsync();

        Assert.Equal("负责协调多个智能体与用户需求。", result.TeamDescription);
        Assert.True(result.AutoApproveProjectGroupCapabilities);
    }

    /// <summary>
    /// zh-CN: 验证更新系统设置时会保存团队描述和简单自动审批开关，确保全局配置能按新的简化合同落库。
    /// en: Verifies updating system settings persists the team description and simple auto-approval toggle through the simplified contract.
    /// </summary>
    [Fact]
    public async Task UpdateSystemAsync_PersistsTeamDescriptionAndAutoApprovalFlag()
    {
        using var context = new TestContext();

        await context.Service.UpdateSystemAsync(new SystemSettingsDto
        {
            TeamName = "OpenStaff",
            TeamDescription = "协作式 AI 软件团队",
            UserName = "主人",
            Language = "zh-CN",
            Timezone = "Asia/Shanghai",
            DefaultTemperature = 0.7,
            DefaultMaxTokens = 4096,
            ResponseStyle = "balanced",
            AutoApproveProjectGroupCapabilities = true,
        });

        var settings = await context.Db.GlobalSettings
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Key, item => item.Value);

        Assert.Equal("协作式 AI 软件团队", settings[SystemSettingsKeys.TeamDescription]);
        Assert.Equal("True", settings[SystemSettingsKeys.ProjectGroupAutoApproveCapabilities]);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly SqliteConnection _connection;

        /// <summary>
        /// zh-CN: 搭建带迁移的内存 SQLite 上下文，尽量贴近真实设置服务的读写路径。
        /// en: Builds an in-memory SQLite context with migrations applied so settings tests exercise the real persistence path.
        /// </summary>
        public TestContext()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            Services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .BuildServiceProvider();

            Db = Services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();

            Service = new SettingsApiService(new SettingsService(new GlobalSettingRepository(Db), Db));
        }

        public ServiceProvider Services { get; }
        public AppDbContext Db { get; }
        public SettingsApiService Service { get; }

        /// <summary>
        /// zh-CN: 释放数据库连接和服务容器，避免内存数据库状态泄漏到其他测试。
        /// en: Disposes the database connection and service container so in-memory state does not leak into other tests.
        /// </summary>
        public void Dispose()
        {
            Db.Dispose();
            Services.Dispose();
            _connection.Dispose();
        }
    }
}

