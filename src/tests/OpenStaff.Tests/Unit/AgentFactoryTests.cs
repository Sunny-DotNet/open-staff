using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenHub.Agents;
using OpenStaff.Agent;
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

public class AgentFactoryTests
{
    private static AgentFactory CreateFactory(params IAgentProvider[] providers)
    {
        return new AgentFactory(providers);
    }

    private static BuiltinAgentProvider CreateBuiltinProvider()
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

    [Fact]
    public void HasProvider_ShouldReturnFalseWhenEmpty()
    {
        var factory = CreateFactory();
        Assert.False(factory.HasProvider("builtin"));
    }

    [Fact]
    public void HasProvider_ShouldReturnTrueWhenRegistered()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        Assert.True(factory.HasProvider("builtin"));
    }

    [Fact]
    public void HasProvider_ShouldReturnFalseForUnknownProvider()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        Assert.False(factory.HasProvider("nonexistent"));
    }

    [Fact]
    public void Providers_ShouldListAllRegisteredProviders()
    {
        var mockProvider = new Mock<IAgentProvider>();
        mockProvider.Setup(p => p.ProviderType).Returns("anthropic");

        var factory = CreateFactory(CreateBuiltinProvider(), mockProvider.Object);

        Assert.Equal(2, factory.Providers.Count);
        Assert.True(factory.HasProvider("builtin"));
        Assert.True(factory.HasProvider("anthropic"));
    }

    [Fact]
    public void Providers_EmptyByDefault()
    {
        var factory = CreateFactory();
        Assert.Empty(factory.Providers);
    }

    [Fact]
    public void Providers_ShouldExcludePlatformsThatOnlyExistAsCapabilities()
    {
        var platform = new PlatformBackedProvider();
        var factory = new AgentFactory(
            new PlatformRegistry([platform]),
            [],
            new ServiceCollection().BuildServiceProvider());

        Assert.Empty(factory.Providers);
        Assert.False(factory.HasProvider(platform.ProviderType));
    }

    [Fact]
    public async Task CreateAgent_ShouldThrowForUnregisteredProvider()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        var role = new AgentRole { JobTitle = "test", Name = "Test", ProviderType = "unknown" };
        var context = new AgentContext { Role = role };
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAgentAsync(role, context));
    }

    [Fact]
    public async Task CreateAgent_ShouldNotFallbackToPlatformImplementedProvider()
    {
        var platform = new PlatformBackedProvider();
        var factory = new AgentFactory(
            new PlatformRegistry([platform]),
            [],
            new ServiceCollection().BuildServiceProvider());
        var role = new AgentRole { JobTitle = "test", Name = "Test", ProviderType = platform.ProviderType };
        var context = new AgentContext { Role = role };

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAgentAsync(role, context));
    }

    [Fact]
    public async Task CreateAgent_ShouldUseRegisteredPlatformBackedProvider()
    {
        var platform = new PlatformBackedProvider();
        var factory = CreateFactory(platform);
        var role = new AgentRole
        {
            JobTitle = "test",
            Name = "Capability Agent",
            ProviderType = platform.ProviderType
        };
        var context = new AgentContext { Role = role };

        var agent = await factory.CreateAgentAsync(role, context);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<IStaffAgent>(agent);
    }

    [Fact]
    public void PlatformRegistry_ShouldResolveProtocolsFromCapabilityInterfaces()
    {
        var registry = new PlatformRegistry([new ProtocolCapabilityPlatform()]);

        var protocol = Assert.Single(registry.GetProtocols());

        Assert.IsType<StubProtocol>(protocol);
    }

    [Fact]
    public async Task CreateAgent_ShouldReturnAIAgentForBuiltin()
    {
        var provider = CreateBuiltinProvider();
        var factory = CreateFactory(provider);
        var role = new AgentRole
        {
            JobTitle = "secretary",
            Name = "Secretary",
            ModelProviderId = Guid.NewGuid(),
            ModelName = "gpt-4o",
        };
        var context = new AgentContext { Role = role };

        var agent = await factory.CreateAgentAsync(role, context);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<IStaffAgent>(agent);
    }

    [Fact]
    public async Task CreateAgent_DefaultsToBuiltinProvider()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        var role = new AgentRole
        {
            JobTitle = "secretary",
            Name = "Secretary",
            ProviderType = null,
            ModelProviderId = Guid.NewGuid(),
            ModelName = "gpt-4o",
        };
        var context = new AgentContext { Role = role };

        var agent = await factory.CreateAgentAsync(role, context);
        Assert.NotNull(agent);
    }

    private sealed class PlatformBackedProvider : IPlatform, IAgentProvider
    {
        public string PlatformKey => ProviderType;
        public string ProviderType => "platform-provider";
        public string DisplayName => "Platform Provider";
        public string? AvatarDataUri => null;

        public Task<IStaffAgent> CreateAgentAsync(AgentRole role, AgentContext context)
        {
            var chatClient = new Mock<IChatClient>().Object;
            AIAgent agent = new ChatClientAgent(chatClient, name: role.Name, instructions: string.Empty);
            return Task.FromResult(agent.AsStaffAgent(new ServiceCollection().BuildServiceProvider()));
        }
    }

    private sealed class ProtocolCapabilityPlatform : IPlatform, IHasProtocol
    {
        public string PlatformKey => "protocol-platform";

        public IProtocol GetProtocol() => new StubProtocol();
    }

    private sealed class StubProtocol : IProtocol
    {
        public bool IsVendor => false;

        public string ProtocolKey => "openai";

        public string ProtocolName => "OpenAI";

        public string Logo => "OpenAI";

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
