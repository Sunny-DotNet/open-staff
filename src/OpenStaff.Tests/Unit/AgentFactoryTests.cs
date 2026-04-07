using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Agent.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using Xunit;

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
        var toolRegistry = new AgentToolRegistry();
        var chatClientFactory = new ChatClientFactory(services.GetRequiredService<ILoggerFactory>());
        var promptLoader = new OpenStaff.Agent.Builtin.Prompts.EmbeddedPromptLoader();
        return new BuiltinAgentProvider(services, toolRegistry, chatClientFactory, promptLoader, services.GetRequiredService<ILoggerFactory>());
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
    public async Task CreateAgent_ShouldThrowForUnregisteredProvider()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        var role = new AgentRole { RoleType = "test", ProviderType = "unknown" };
        var resolved = new ResolvedProvider { Account = new ProviderAccount { ProtocolType = "openai", Name = "Test" }, ApiKey = "k" };
        var context = new AgentContext { Role = role };
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAgentAsync(role, context, resolved));
    }

    [Fact]
    public async Task CreateAgent_ShouldReturnAIAgentForBuiltin()
    {
        var provider = CreateBuiltinProvider();
        var factory = CreateFactory(provider);
        var role = new AgentRole
        {
            RoleType = "secretary",
            Name = "Secretary",
        };
        var resolved = new ResolvedProvider
        {
            Account = new ProviderAccount { ProtocolType = "openai", Name = "Test" },
            ApiKey = "test-key"
        };
        var context = new AgentContext { Role = role };

        var agent = await factory.CreateAgentAsync(role, context, resolved);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public async Task CreateAgent_DefaultsToBuiltinProvider()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        var role = new AgentRole
        {
            RoleType = "secretary",
            Name = "Secretary",
            ProviderType = null,
        };
        var resolved = new ResolvedProvider
        {
            Account = new ProviderAccount { ProtocolType = "openai", Name = "Test" },
            ApiKey = "test-key"
        };
        var context = new AgentContext { Role = role };

        var agent = await factory.CreateAgentAsync(role, context, resolved);
        Assert.NotNull(agent);
    }
}
