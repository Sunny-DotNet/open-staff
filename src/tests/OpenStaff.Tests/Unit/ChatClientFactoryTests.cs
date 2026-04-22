using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agent.Builtin;
using OpenStaff.Provider;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Protocols;
using OpenStaff.Provider.Platforms;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public class ChatClientFactoryTests
{
    /// <summary>
    /// zh-CN: 验证 builtin 分发器会先通过协议工厂解析协议实例和模型元数据，再通过平台 capability 激活独立的 ChatClientFactory。
    /// en: Verifies the builtin dispatcher first resolves protocol metadata through the protocol factory and then activates a standalone chat-client factory from the platform capability.
    /// </summary>
    [Fact]
    public async Task CreateAsync_UsesPlatformChatClientFactory()
    {
        var registry = new CapabilityFactoryRegistry(new Mock<IChatClient>().Object);
        var protocol = new FakeProtocol([
            new ModelInfo("gpt-5.4-mini", "github-copilot", ModelProtocolType.OpenAIResponse)
        ]);
        var factory = CreateDispatcher(
            protocol,
            new ChatClientFactoryPlatform(new DelegatingChatClientFactory(registry)));

        var provider = CreateResolvedProvider("""
            {"baseUrl":"https://api.individual.githubcopilot.com","apiKey":"test-api-key"}
            """);
        var result = await factory.CreateAsync(provider, "gpt-5.4-mini");

        Assert.Same(registry.ChatClient, result);
        Assert.NotNull(registry.LastRequest);
        Assert.Equal("gpt-5.4-mini", registry.LastRequest!.ModelId);
    }

    /// <summary>
    /// zh-CN: 验证分发器会缓存协议层返回的模型元数据，避免同一账号重复枚举模型列表。
    /// en: Verifies the dispatcher caches protocol-layer model metadata so the same account does not enumerate models repeatedly.
    /// </summary>
    [Fact]
    public async Task ResolveModelInfoAsync_UsesProtocolMetadataAndCachesResults()
    {
        var protocol = new FakeProtocol([
            new ModelInfo("gpt-5.4-mini", "github-copilot", ModelProtocolType.OpenAIResponse)
        ]);
        var factory = CreateDispatcher(protocol);
        var provider = CreateResolvedProvider();

        var first = await factory.ResolveModelInfoAsync(provider, protocol, "gpt-5.4-mini");
        var second = await factory.ResolveModelInfoAsync(provider, protocol, "gpt-5.4-mini");

        Assert.Equal(ModelProtocolType.OpenAIResponse, first.ModelProtocols);
        Assert.Equal(ModelProtocolType.OpenAIResponse, second.ModelProtocols);
        Assert.Equal(1, protocol.ModelsCallCount);
    }

    /// <summary>
    /// zh-CN: 验证当协议找不到模型元数据时，分发器会返回一个只包含模型标识的兜底条目，而不是直接伪造协议能力。
    /// en: Verifies the dispatcher returns a fallback entry containing only the model identifier when protocol metadata cannot be found instead of fabricating protocol capabilities.
    /// </summary>
    [Fact]
    public async Task ResolveModelInfoAsync_ReturnsFallbackEntryWhenModelIsMissing()
    {
        var protocol = new FakeProtocol([]);
        var factory = CreateDispatcher(protocol);
        var provider = CreateResolvedProvider();

        var modelInfo = await factory.ResolveModelInfoAsync(provider, protocol, "unknown-model");

        Assert.Equal("unknown-model", modelInfo.ModelSlug);
        Assert.Equal(ModelProtocolType.None, modelInfo.ModelProtocols);
    }

    /// <summary>
    /// zh-CN: 验证平台 capability 返回的不支持工厂会把明确异常直接透出。
    /// en: Verifies the dispatcher surfaces the explicit error directly when the platform capability points to an unsupported factory.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ThrowsWhenPlatformChatClientFactoryIsUnsupported()
    {
        var protocol = new FakeProtocol([]);
        var factory = CreateDispatcher(
            protocol,
            new ChatClientFactoryPlatform(new UnsupportedDelegatingChatClientFactory()));
        var provider = CreateResolvedProvider();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => factory.CreateAsync(provider, "gpt-5.4-mini"));

        Assert.Contains("github-copilot", ex.Message);
    }

    /// <summary>
    /// zh-CN: 创建只依赖协议工厂、平台 capability 和最小 DI 容器的 ChatClientFactory 分发器。
    /// en: Creates a ChatClientFactory dispatcher backed only by the protocol factory, platform capability, and a minimal DI container.
    /// </summary>
    private static ChatClientFactory CreateDispatcher(
        IProtocol protocol,
        IPlatform? platform = null,
        Action<IServiceCollection>? configureServices = null,
        Action<Mock<IProtocolFactory>>? configureProtocolFactory = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProviderAccountRepository>(_ =>
            {
                var repository = new Mock<IProviderAccountRepository>();
                repository
                    .Setup(item => item.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .Returns((Guid id, CancellationToken _) => ValueTask.FromResult<ProviderAccount?>(new ProviderAccount
                    {
                        Id = id,
                        Name = "GitHub Copilot",
                        ProtocolType = "github-copilot",
                        UpdatedAt = DateTime.UtcNow
                    }));
                return repository.Object;
            })
            .AddSingleton<ICurrentProviderDetail>(new Mock<ICurrentProviderDetail>().Object);
        configureServices?.Invoke(services);

        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var protocolFactory = new Mock<IProtocolFactory>();
        protocolFactory
            .Setup(factory => factory.CreateProtocolWithEnv("github-copilot", It.IsAny<string>()))
            .Returns(protocol);
        configureProtocolFactory?.Invoke(protocolFactory);

        return new ChatClientFactory(
            loggerFactory,
            protocolFactory.Object,
            new PlatformRegistry(platform is null ? [] : [platform]),
            serviceProvider);
    }

    /// <summary>
    /// zh-CN: 构造稳定的 GitHub Copilot provider 快照，供 dispatcher 测试复用。
    /// en: Builds a stable GitHub Copilot provider snapshot reused by dispatcher tests.
    /// </summary>
    private static ResolvedProvider CreateResolvedProvider(string? envConfigJson = "{}") => new()
    {
        Account = new ProviderAccount
        {
            Id = Guid.NewGuid(),
            Name = "GitHub Copilot",
            ProtocolType = "github-copilot",
            UpdatedAt = DateTime.UtcNow
        },
        ApiKey = "test-api-key",
        BaseUrl = "https://api.individual.githubcopilot.com",
        EnvConfigJson = envConfigJson
    };

    /// <summary>
    /// zh-CN: 通过平台 capability 暴露的独立 factory，记录反序列化的协议环境并返回固定聊天客户端。
    /// en: Standalone factory exposed by the platform capability that records the deserialized protocol env and returns a fixed chat client.
    /// </summary>
    private sealed class DelegatingChatClientFactory(CapabilityFactoryRegistry registry) : IChatClientFactory
    {
        public Task<IChatClient> CreateAsync(
            ChatClientCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            registry.LastRequest = request;
            return Task.FromResult(registry.ChatClient);
        }
    }

    /// <summary>
    /// zh-CN: 使用固定模型目录模拟协议实现，并记录查询次数以验证 dispatcher 缓存是否生效；协议本身不再暴露聊天客户端工厂。
    /// en: Simulates a protocol with a fixed model catalog and records lookup count so dispatcher caching can be verified; the protocol itself no longer exposes a chat-client factory.
    /// </summary>
    private sealed class FakeProtocol(IEnumerable<ModelInfo> models) : IProtocol
    {
        private readonly IReadOnlyList<ModelInfo> _models = models.ToList();

        public int ModelsCallCount { get; private set; }

        public bool IsVendor => false;

        public string ProtocolKey => "github-copilot";

        public string ProtocolName => "GitHub Copilot";

        public string Logo => "GitHubCopilot";

        public Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
        {
            ModelsCallCount++;
            return Task.FromResult<IEnumerable<ModelInfo>>(_models);
        }
    }

    private sealed class ChatClientFactoryPlatform(IChatClientFactory chatClientFactory) : IPlatform, IHasChatClientFactory
    {
        public string PlatformKey => "github-copilot";

        public IChatClientFactory GetChatClientFactory()
            => chatClientFactory;
    }

    private sealed class UnsupportedDelegatingChatClientFactory : IChatClientFactory
    {
        public Task<IChatClient> CreateAsync(ChatClientCreateRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Provider protocol 'github-copilot' does not expose a runtime IChatClient factory.");
    }

    private sealed class CapabilityFactoryRegistry(IChatClient chatClient)
    {
        public IChatClient ChatClient { get; } = chatClient;

        public ChatClientCreateRequest? LastRequest { get; set; }
    }
}
