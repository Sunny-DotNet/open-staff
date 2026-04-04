using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agents;
using OpenStaff.Agents.Prompts;
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
        var promptLoader = new EmbeddedPromptLoader();
        var aiAgentFactory = new AIAgentFactory(new ChatClientFactory(services.GetRequiredService<ILoggerFactory>()), services.GetRequiredService<ILoggerFactory>());
        return new AgentFactory(services, toolRegistry, promptLoader, aiAgentFactory);
    }

    private static RoleConfig CreateRoleConfig(string roleType) => new()
    {
        RoleType = roleType,
        Name = roleType,
        SystemPrompt = $"{roleType}.system",
        IsBuiltin = true
    };

    [Fact]
    public void IsRegistered_ShouldReturnFalseWhenNothingRegistered()
    {
        var factory = CreateFactory();
        Assert.False(factory.IsRegistered(BuiltinRoleTypes.Communicator));
    }

    [Fact]
    public void RegisterRole_ShouldMakeRoleRegistered()
    {
        var factory = CreateFactory();
        factory.RegisterRole(CreateRoleConfig(BuiltinRoleTypes.Communicator));

        Assert.True(factory.IsRegistered(BuiltinRoleTypes.Communicator));
    }

    [Fact]
    public void IsRegistered_ShouldReturnFalseForUnknownRole()
    {
        var factory = CreateFactory();
        factory.RegisterRole(CreateRoleConfig(BuiltinRoleTypes.Producer));

        Assert.False(factory.IsRegistered("nonexistent_role"));
    }

    [Fact]
    public void RegisteredRoleTypes_ShouldListAllRegisteredRoles()
    {
        var factory = CreateFactory();
        factory.RegisterRole(CreateRoleConfig(BuiltinRoleTypes.Communicator));
        factory.RegisterRole(CreateRoleConfig(BuiltinRoleTypes.Producer));
        factory.RegisterRole(CreateRoleConfig(BuiltinRoleTypes.Debugger));

        var roles = factory.RegisteredRoleTypes;
        Assert.Equal(3, roles.Count);
        Assert.Contains(BuiltinRoleTypes.Communicator, roles);
        Assert.Contains(BuiltinRoleTypes.Producer, roles);
        Assert.Contains(BuiltinRoleTypes.Debugger, roles);
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
        factory.RegisterRole(CreateRoleConfig(BuiltinRoleTypes.Architect));

        var agent = factory.CreateAgent(BuiltinRoleTypes.Architect);
        Assert.NotNull(agent);
        Assert.IsType<StandardAgent>(agent);
        Assert.Equal(BuiltinRoleTypes.Architect, agent.RoleType);
    }

    [Fact]
    public void GetRoleConfig_ShouldReturnConfigForRegisteredRole()
    {
        var factory = CreateFactory();
        var config = CreateRoleConfig(BuiltinRoleTypes.Communicator);
        factory.RegisterRole(config);

        var result = factory.GetRoleConfig(BuiltinRoleTypes.Communicator);
        Assert.NotNull(result);
        Assert.Equal(BuiltinRoleTypes.Communicator, result!.RoleType);
    }

    [Fact]
    public void GetRoleConfig_ShouldReturnNullForUnknownRole()
    {
        var factory = CreateFactory();
        Assert.Null(factory.GetRoleConfig("unknown"));
    }
}
