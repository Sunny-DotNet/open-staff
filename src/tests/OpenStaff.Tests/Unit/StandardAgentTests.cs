using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agents;
using OpenStaff.Agent.Builtin;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Protocols;
using OpenStaff.Provider;
using Xunit;
using OpenStaff.Provider.Platforms;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public class BuiltinAgentProviderTests
{
    /// <summary>
    /// zh-CN: 创建最小可用的内置代理提供程序，复用真实注册表，以覆盖内置角色解析路径。
    /// en: Creates a minimally wired built-in agent provider with the real registry so tests cover the built-in role resolution path.
    /// </summary>
    private static BuiltinAgentProvider CreateProvider()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var protocolFactory = new Mock<IProtocolFactory>();
        var providerAccounts = new Mock<IProviderAccountRepository>();
        providerAccounts
            .Setup(repository => repository.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => ValueTask.FromResult<ProviderAccount?>(new ProviderAccount { Id = id, ProtocolType = "openai", Name = "Test" }));
        var chatClientFactory = new ChatClientFactory(
            services.GetRequiredService<ILoggerFactory>(),
            providerAccounts.Object,
            new Mock<ICurrentProviderDetail>().Object,
            protocolFactory.Object,
            new PlatformRegistry([new OpenAIChatClientFactoryPlatform()]),
            services);
        return new BuiltinAgentProvider(services, chatClientFactory, new Mock<IAgentPromptGenerator>().Object, services.GetRequiredService<ILoggerFactory>());
    }

    /// <summary>
    /// zh-CN: 生成默认角色实体，便于测试在不同 roleType 下的配置查找和代理创建行为。
    /// en: Creates a default role entity so tests can exercise configuration lookup and agent creation with different role types.
    /// </summary>
    private static AgentRole CreateRole(string roleType = "secretary") => new()
    {
        Name = roleType,
        JobTitle = roleType,
        ModelProviderId = Guid.NewGuid(),
        ModelName = "gpt-4o",
    };

    /// <summary>
    /// zh-CN: 验证内置代理提供程序对外暴露固定的 provider 类型，用于路由和筛选。
    /// en: Verifies the built-in provider exposes the expected provider type string used for routing and filtering.
    /// </summary>
    [Fact]
    public void ProviderType_IsBuiltin()
    {
        var provider = CreateProvider();
        Assert.Equal("builtin", provider.ProviderType);
    }

    /// <summary>
    /// zh-CN: 验证在依赖完整且角色有效时，提供程序能够创建可执行的 StaffAgent 实例。
    /// en: Verifies the provider can create a usable staff-agent instance when dependencies are present and the role is valid.
    /// </summary>
    [Fact]
    public async Task CreateAgent_ReturnsAIAgent()
    {
        var provider = CreateProvider();
        var role = CreateRole();
        var context = new AgentContext { Role = role };

        var agent = await provider.CreateAgentAsync(role, context);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<IStaffAgent>(agent);
    }

    /// <summary>
    /// zh-CN: 验证缺少协议账号时会快速失败，避免创建一个没有模型来源的代理。
    /// en: Verifies creation fails fast when the provider account is missing so an agent cannot be built without a model source.
    /// </summary>
    [Fact]
    public async Task CreateAgent_ThrowsWithoutProviderAccount()
    {
        var provider = CreateProvider();
        var role = new AgentRole { JobTitle = "secretary", Name = "Secretary" };
        var context = new AgentContext { Role = role };

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreateAgentAsync(role, context));
    }

    /// <summary>
    /// zh-CN: 验证自定义数据库角色会优先使用角色实体上的配置，而不是要求它存在于内置角色表中。
    /// en: Verifies a custom database-backed role uses the configuration on the role entity itself instead of requiring a built-in role entry.
    /// </summary>
    [Fact]
    public async Task CreateAgent_UsesDbRoleConfigForCustomRole()
    {
        var provider = CreateProvider();
        var role = new AgentRole
        {
            JobTitle = "my_custom_role",
            Name = "Custom Role",
            Description = "You are a custom agent.",
            ModelProviderId = Guid.NewGuid(),
            ModelName = "gpt-4o",
        };
        var context = new AgentContext { Role = role };

        var agent = await provider.CreateAgentAsync(role, context);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<IStaffAgent>(agent);
    }

    [Fact]
    public async Task CreateAgent_ProjectGroupSecretary_EnablesNativeStructuredOutputModeWhenModelSupportsIt()
    {
        var promptGenerator = new Mock<IAgentPromptGenerator>();
        Dictionary<string, object>? capturedExtraConfig = null;
        promptGenerator
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .Callback<AgentRole, AgentContext, CancellationToken>((_, context, _) => capturedExtraConfig = new Dictionary<string, object>(context.ExtraConfig))
            .ReturnsAsync("prompt");

        var provider = CreateProjectGroupProvider(
            promptGenerator.Object,
            [new ModelInfo("gpt-4o", "openai", ModelProtocolType.OpenAIChatCompletions, supportsStructuredOutputs: true)]);
        var role = CreateRole();
        var context = new AgentContext
        {
            Role = role,
            Scene = SceneType.ProjectGroup,
            ExtraConfig = new Dictionary<string, object>()
        };

        await provider.CreateAgentAsync(role, context);

        Assert.NotNull(capturedExtraConfig);
        Assert.Equal(
            ProjectGroupOrchestratorContract.NativeJsonSchemaOutputMode,
            capturedExtraConfig![ProjectGroupOrchestratorContract.OutputModeExtraConfigKey]);
    }

    [Fact]
    public async Task CreateAgent_ProjectGroupSecretary_FallsBackToTaggedStructuredOutputModeWhenModelDoesNotAdvertiseSupport()
    {
        var promptGenerator = new Mock<IAgentPromptGenerator>();
        Dictionary<string, object>? capturedExtraConfig = null;
        promptGenerator
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .Callback<AgentRole, AgentContext, CancellationToken>((_, context, _) => capturedExtraConfig = new Dictionary<string, object>(context.ExtraConfig))
            .ReturnsAsync("prompt");

        var provider = CreateProjectGroupProvider(
            promptGenerator.Object,
            [new ModelInfo("gpt-4o", "openai", ModelProtocolType.OpenAIChatCompletions, supportsStructuredOutputs: false)]);
        var role = CreateRole();
        var context = new AgentContext
        {
            Role = role,
            Scene = SceneType.ProjectGroup,
            ExtraConfig = new Dictionary<string, object>()
        };

        await provider.CreateAgentAsync(role, context);

        Assert.NotNull(capturedExtraConfig);
        Assert.Equal(
            ProjectGroupOrchestratorContract.TaggedJsonFallbackOutputMode,
            capturedExtraConfig![ProjectGroupOrchestratorContract.OutputModeExtraConfigKey]);
    }

    /// <summary>
    /// zh-CN: 提供一个最小的协议测试替身，只为 agent 装配测试暴露模型元数据入口。
    /// en: Minimal protocol test double that exposes only the model-metadata entry point needed by agent assembly tests.
    /// </summary>
    private static BuiltinAgentProvider CreateProjectGroupProvider(
        IAgentPromptGenerator promptGenerator,
        IEnumerable<ModelInfo> models)
    {
        var accountId = Guid.NewGuid();
        var protocol = new StubProtocol(models);
        var protocolFactory = new Mock<IProtocolFactory>();
        protocolFactory
            .Setup(item => item.CreateProtocolWithEnv("openai", It.IsAny<string>()))
            .Returns(protocol);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProviderResolver>(_ =>
            {
                var resolver = new Mock<IProviderResolver>();
                resolver.Setup(item => item.ResolveAsync(accountId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ResolvedProvider
                    {
                        Account = new ProviderAccount { Id = accountId, ProtocolType = "openai", Name = "Test" },
                        EnvConfigJson = "{}"
                    });
                return resolver.Object;
            })
            .AddSingleton<IProtocolFactory>(protocolFactory.Object)
            .BuildServiceProvider();

        var providerAccounts = new Mock<IProviderAccountRepository>();
        var chatClientFactory = new ChatClientFactory(
            services.GetRequiredService<ILoggerFactory>(),
            providerAccounts.Object,
            new Mock<ICurrentProviderDetail>().Object,
            protocolFactory.Object,
            new PlatformRegistry([new OpenAIChatClientFactoryPlatform()]),
            services);

        return new BuiltinAgentProvider(
            services,
            chatClientFactory,
            promptGenerator,
            services.GetRequiredService<ILoggerFactory>());
    }

    private sealed class StubProtocol(IEnumerable<ModelInfo>? models = null) : IProtocol
    {
        private readonly IEnumerable<ModelInfo> _models = models ?? [];

        public bool IsVendor => false;

        public string ProtocolKey => "openai";

        public string ProtocolName => "OpenAI";

        public string Logo => "OpenAI";

        /// <summary>
        /// zh-CN: 这些装配测试不会真正依赖模型目录，因此这里返回空列表即可。
        /// en: These assembly tests do not rely on the model catalog, so an empty list is sufficient here.
        /// </summary>
        public Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_models);
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
    /// zh-CN: 提供一个最小的 OpenAI provider factory 测试替身，只为 agent 装配测试返回可用的聊天客户端。
    /// en: Minimal OpenAI provider-factory test double that only returns a usable chat client for agent assembly tests.
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
}
