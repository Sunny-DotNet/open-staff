using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agents;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class AgentFactoryTests
{
    private static AgentFactory CreateFactory()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new AgentFactory(services);
    }

    [Fact]
    public void IsRegistered_ShouldReturnFalseWhenNothingRegistered()
    {
        var factory = CreateFactory();
        Assert.False(factory.IsRegistered(BuiltinRoleTypes.Communicator));
    }

    [Fact]
    public void RegisterAgent_ShouldMakeRoleRegistered()
    {
        var factory = CreateFactory();
        factory.RegisterAgent<FakeAgent>(BuiltinRoleTypes.Communicator);

        Assert.True(factory.IsRegistered(BuiltinRoleTypes.Communicator));
    }

    [Fact]
    public void IsRegistered_ShouldReturnFalseForUnknownRole()
    {
        var factory = CreateFactory();
        factory.RegisterAgent<FakeAgent>(BuiltinRoleTypes.Producer);

        Assert.False(factory.IsRegistered("nonexistent_role"));
    }

    [Fact]
    public void RegisteredRoleTypes_ShouldListAllRegisteredRoles()
    {
        var factory = CreateFactory();
        factory.RegisterAgent<FakeAgent>(BuiltinRoleTypes.Communicator);
        factory.RegisterAgent<FakeAgent>(BuiltinRoleTypes.Producer);
        factory.RegisterAgent<FakeAgent>(BuiltinRoleTypes.Debugger);

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
    public void CreateAgent_ShouldReturnAgentInstance()
    {
        var factory = CreateFactory();
        factory.RegisterAgent<FakeAgent>(BuiltinRoleTypes.Architect);

        var agent = factory.CreateAgent(BuiltinRoleTypes.Architect);
        Assert.NotNull(agent);
        Assert.IsType<FakeAgent>(agent);
    }

    /// <summary>
    /// Minimal IAgent implementation for testing.
    /// </summary>
    private class FakeAgent : IAgent
    {
        public string RoleType => "fake";
        public string Status => "idle";

        public Task InitializeAsync(AgentContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentResponse());

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
