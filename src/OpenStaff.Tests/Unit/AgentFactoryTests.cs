using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agents;
using OpenStaff.Agents.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class AgentFactoryTests
{
    private static AgentFactory CreateFactory()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var toolRegistry = new AgentToolRegistry();
        var aiAgentFactory = new AIAgentFactory(new ChatClientFactory(services.GetRequiredService<ILoggerFactory>()), services.GetRequiredService<ILoggerFactory>());
        return new AgentFactory(services, toolRegistry, aiAgentFactory, []);
    }

    private static RoleConfig CreateRoleConfig(string roleType) => new()
    {
        RoleType = roleType,
        Name = roleType,
        SystemPrompt = "You are a test agent.",
        IsBuiltin = true
    };

    [Fact]
    public void IsRegistered_ShouldReturnFalseWhenNothingRegistered()
    {
        var factory = CreateFactory();
        Assert.False(factory.IsRegistered("secretary"));
    }

    [Fact]
    public void RegisterRole_ShouldMakeRoleRegistered()
    {
        var factory = CreateFactory();
        factory.RegisterRole(CreateRoleConfig("secretary"));

        Assert.True(factory.IsRegistered("secretary"));
    }

    [Fact]
    public void IsRegistered_ShouldReturnFalseForUnknownRole()
    {
        var factory = CreateFactory();
        factory.RegisterRole(CreateRoleConfig("producer"));

        Assert.False(factory.IsRegistered("nonexistent_role"));
    }

    [Fact]
    public void RegisteredRoleTypes_ShouldListAllRegisteredRoles()
    {
        var factory = CreateFactory();
        factory.RegisterRole(CreateRoleConfig("secretary"));
        factory.RegisterRole(CreateRoleConfig("producer"));
        factory.RegisterRole(CreateRoleConfig("debugger"));

        var roles = factory.RegisteredRoleTypes;
        Assert.Equal(3, roles.Count);
        Assert.Contains("secretary", roles);
        Assert.Contains("producer", roles);
        Assert.Contains("debugger", roles);
    }

    [Fact]
    public void RegisteredRoleTypes_EmptyByDefault()
    {
        var factory = CreateFactory();
        Assert.Empty(factory.RegisteredRoleTypes);
    }

    [Fact]
    public void CreateAgent_ShouldThrowForUnregisteredRole()
    {
        var factory = CreateFactory();
        Assert.Throws<InvalidOperationException>(() => factory.CreateAgent("unknown_role"));
    }

    [Fact]
    public void CreateAgent_ShouldReturnStandardAgentInstance()
    {
        var factory = CreateFactory();
        factory.RegisterRole(CreateRoleConfig("architect"));

        var agent = factory.CreateAgent("architect");
        Assert.NotNull(agent);
        Assert.IsType<StandardAgent>(agent);
        Assert.Equal("architect", agent.RoleType);
    }

    [Fact]
    public void GetRoleConfig_ShouldReturnConfigForRegisteredRole()
    {
        var factory = CreateFactory();
        var config = CreateRoleConfig("secretary");
        factory.RegisterRole(config);

        var result = factory.GetRoleConfig("secretary");
        Assert.NotNull(result);
        Assert.Equal("secretary", result!.RoleType);
    }

    [Fact]
    public void GetRoleConfig_ShouldReturnNullForUnknownRole()
    {
        var factory = CreateFactory();
        Assert.Null(factory.GetRoleConfig("unknown"));
    }
}
