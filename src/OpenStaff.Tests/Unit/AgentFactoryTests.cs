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
        var aiAgentFactory = new AIAgentFactory(chatClientFactory, services.GetRequiredService<ILoggerFactory>());
        var promptLoader = new OpenStaff.Agent.Builtin.Prompts.EmbeddedPromptLoader();
        return new BuiltinAgentProvider(services, toolRegistry, aiAgentFactory, promptLoader);
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
    public void CreateAgent_ShouldThrowForUnregisteredProvider()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        var role = new AgentRole { RoleType = "test", ProviderType = "unknown" };
        Assert.Throws<InvalidOperationException>(() => factory.CreateAgent(role));
    }

    [Fact]
    public void CreateAgent_ShouldReturnStandardAgentForBuiltin()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        var role = new AgentRole { RoleType = "secretary", Name = "Secretary" };

        var agent = factory.CreateAgent(role);
        Assert.NotNull(agent);
        Assert.IsType<StandardAgent>(agent);
        Assert.Equal("secretary", agent.RoleType);
    }

    [Fact]
    public void CreateAgent_DefaultsToBuiltinProvider()
    {
        var factory = CreateFactory(CreateBuiltinProvider());
        // ProviderType is null → defaults to "builtin"
        var role = new AgentRole { RoleType = "secretary", Name = "Secretary", ProviderType = null };

        var agent = factory.CreateAgent(role);
        Assert.NotNull(agent);
    }
}
