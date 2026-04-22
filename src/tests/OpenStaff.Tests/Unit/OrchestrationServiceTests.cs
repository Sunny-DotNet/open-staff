using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Application.Orchestration.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Provider;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Protocols;
using OpenStaff.Repositories;
using Xunit;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Tests.Unit;

public class OrchestrationServiceTests
{
    /// <summary>
    /// zh-CN: 创建使用内置角色配置的代理提供程序，供编排测试覆盖真实注册路径。
    /// en: Creates an agent provider that uses built-in role configuration so orchestration tests exercise the real registration path.
    /// </summary>
    private static BuiltinAgentProvider CreateBuiltinProvider()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProviderAccountRepository>(_ =>
            {
                var repository = new Mock<IProviderAccountRepository>();
                repository
                    .Setup(item => item.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .Returns((Guid id, CancellationToken _) => ValueTask.FromResult<ProviderAccount?>(new ProviderAccount { Id = id, ProtocolType = "openai", Name = "Test" }));
                return repository.Object;
            })
            .AddSingleton<ICurrentProviderDetail>(new Mock<ICurrentProviderDetail>().Object)
            .BuildServiceProvider();
        var protocolFactory = new Mock<IProtocolFactory>();
        protocolFactory
            .Setup(factory => factory.CreateProtocolWithEnv(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new StubProtocol());
        var chatClientFactory = new ChatClientFactory(
            services.GetRequiredService<ILoggerFactory>(),
            protocolFactory.Object,
            new PlatformRegistry([new OpenAIChatClientFactoryPlatform()]),
            services);
        return new BuiltinAgentProvider(services, chatClientFactory, new Mock<IAgentPromptGenerator>().Object, services.GetRequiredService<ILoggerFactory>());
    }

    /// <summary>
    /// zh-CN: 构建只包含内置代理提供程序的工厂，让测试聚焦编排层契约而非插件组合。
    /// en: Builds a factory that contains only the built-in agent provider so the tests stay focused on orchestration contracts instead of plugin composition.
    /// </summary>
    private static AgentFactory CreateFactoryWithBuiltin()
    {
        return new AgentFactory(new IAgentProvider[] { CreateBuiltinProvider() });
    }

    /// <summary>
    /// zh-CN: 以最小的通知、数据库与工具依赖搭建编排服务测试桩，覆盖公共入口而不启动完整宿主。
    /// en: Creates an orchestration service test harness with minimal notification, database, and tool dependencies so public entry points can be exercised without a full host.
    /// </summary>
    private static OrchestrationService CreateService(AgentFactory? factory = null)
    {
        factory ??= CreateFactoryWithBuiltin();

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<OrchestrationService>>().Object;
        var agentMcpToolServiceMock = new Mock<IAgentMcpToolService>();
        agentMcpToolServiceMock
            .Setup(service => service.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IProjectRepository, ProjectRepository>()
            .AddScoped<IAgentRoleRepository, AgentRoleRepository>()
            .BuildServiceProvider();

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        }

        return new OrchestrationService(
            factory,
            services.GetRequiredService<IServiceScopeFactory>(),
            agentMcpToolServiceMock.Object,
            notificationMock.Object,
            logger);
    }

    /// <summary>
    /// zh-CN: 验证服务继续实现 IOrchestrator 契约，保护依赖注入与调用方的类型预期。
    /// en: Verifies that the service still implements the IOrchestrator contract, protecting DI registrations and caller type expectations.
    /// </summary>
    [Fact]
    public void ImplementsIOrchestrator()
    {
        var service = CreateService();
        Assert.IsAssignableFrom<IOrchestrator>(service);
    }

    /// <summary>
    /// zh-CN: 验证查询未知项目的代理状态时返回空集合，而不是抛出异常打断上层流程。
    /// en: Verifies that requesting agent statuses for an unknown project returns an empty set instead of throwing and interrupting callers.
    /// </summary>
    [Fact]
    public async Task GetAgentStatusesAsync_UnknownProject_ReturnsEmptyList()
    {
        var service = CreateService();

        var statuses = await service.GetAgentStatusesAsync(Guid.NewGuid());

        Assert.Empty(statuses);
    }

    /// <summary>
    /// zh-CN: 为编排测试提供最小协议替身，只暴露 builtin provider 创建代理时必需的模型元数据入口。
    /// en: Minimal protocol double used by orchestration tests that exposes only the model-metadata entry point required when the builtin provider creates an agent.
    /// </summary>
    private sealed class StubProtocol : IProtocol
    {
        public bool IsVendor => false;

        public string ProtocolKey => "openai";

        public string ProtocolName => "OpenAI";

        public string Logo => "OpenAI";

        /// <summary>
        /// zh-CN: 编排测试不依赖模型目录，这里返回空列表即可。
        /// en: Orchestration tests do not rely on the model catalog, so an empty list is sufficient here.
        /// </summary>
        public Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<ModelInfo>>([]);
        }
    }

    private sealed class OpenAIChatClientFactoryPlatform : IPlatform, IHasChatClientFactory, IHasProtocol
    {
        public string PlatformKey => "openai";

        public IProtocol GetProtocol() => new StubProtocol();

        public IChatClientFactory GetChatClientFactory()
            => new StubOpenAIChatClientFactory();
    }

    /// <summary>
    /// zh-CN: 为编排测试提供最小的 openai provider factory，让测试专注于编排流程而非协议发现。
    /// en: Minimal openai provider factory used by orchestration tests so they stay focused on orchestration rather than protocol discovery.
    /// </summary>
    private sealed class StubOpenAIChatClientFactory : IChatClientFactory
    {
        public Task<IChatClient> CreateAsync(
            ChatClientCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Mock<IChatClient>().Object);
        }
    }

    /// <summary>
    /// zh-CN: 回归测试公共 API 不再暴露旧的执行入口，避免外部代码继续依赖已移除的迁移兼容面。
    /// en: Regression test ensuring the public API no longer exposes legacy execution entry points, preventing external callers from relying on removed compatibility surface.
    /// </summary>
    [Fact]
    public void PublicSurface_NoLongerExposesLegacyExecutionMethods()
    {
        var interfaceMethods = typeof(IOrchestrator)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        var serviceMethods = typeof(OrchestrationService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("HandleUserInputAsync", interfaceMethods);
        Assert.DoesNotContain("RouteToAgentAsync", interfaceMethods);
        Assert.DoesNotContain("HandleUserInputAsync", serviceMethods);
        Assert.DoesNotContain("RouteToAgentAsync", serviceMethods);
    }
}

