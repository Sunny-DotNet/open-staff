using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agents;
using OpenStaff.Agents.Orchestrator;
using OpenStaff.Agents.Prompts;
using OpenStaff.Agents.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Events;
using OpenStaff.Core.Models;
using OpenStaff.Core.Orchestration;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class OrchestrationServiceTests
{
    private static AgentFactory CreateFactoryWithRoles(params string[] roleTypes)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var toolRegistry = new AgentToolRegistry();
        var promptLoader = new EmbeddedPromptLoader();
        var aiAgentFactory = new AIAgentFactory(services.GetRequiredService<ILoggerFactory>());
        var factory = new AgentFactory(services, toolRegistry, promptLoader, aiAgentFactory);

        foreach (var roleType in roleTypes)
        {
            factory.RegisterRole(new RoleConfig
            {
                RoleType = roleType,
                Name = roleType,
                SystemPrompt = $"{roleType}.system",
                IsBuiltin = true,
                Tools = new List<string>()
            });
        }

        return factory;
    }

    private static OrchestrationService CreateService(AgentFactory? factory = null)
    {
        factory ??= CreateFactoryWithRoles(
            BuiltinRoleTypes.Orchestrator,
            BuiltinRoleTypes.Communicator,
            BuiltinRoleTypes.DecisionMaker,
            BuiltinRoleTypes.Architect,
            BuiltinRoleTypes.Producer,
            BuiltinRoleTypes.Debugger);

        var eventPublisherMock = new Mock<IEventPublisher>();
        eventPublisherMock
            .Setup(e => e.PublishAsync(It.IsAny<AgentEventData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var scopedSpMock = new Mock<IServiceProvider>();
        scopedSpMock.Setup(sp => sp.GetService(typeof(IEventPublisher))).Returns(eventPublisherMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(scopedSpMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var logger = new Mock<ILogger<OrchestrationService>>().Object;

        return new OrchestrationService(factory, scopeFactoryMock.Object, logger);
    }

    [Fact]
    public void ImplementsIOrchestrator()
    {
        var service = CreateService();
        Assert.IsAssignableFrom<IOrchestrator>(service);
    }

    [Fact]
    public async Task InitializeProjectAgentsAsync_CreatesAgentsForProject()
    {
        var service = CreateService();
        var projectId = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(projectId);

        var statuses = await service.GetAgentStatusesAsync(projectId);
        // Orchestrator is excluded from initialization (created on-demand)
        Assert.True(statuses.Count > 0);
    }

    [Fact]
    public async Task GetAgentStatusesAsync_ReturnsStatusForAllProjectAgents()
    {
        var factory = CreateFactoryWithRoles(
            BuiltinRoleTypes.Communicator,
            BuiltinRoleTypes.Architect,
            BuiltinRoleTypes.Producer);

        var service = CreateService(factory);
        var projectId = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(projectId);
        var statuses = await service.GetAgentStatusesAsync(projectId);

        Assert.Equal(3, statuses.Count);
        var roleTypes = statuses.Select(s => s.RoleType).ToHashSet();
        Assert.Contains(BuiltinRoleTypes.Communicator, roleTypes);
        Assert.Contains(BuiltinRoleTypes.Architect, roleTypes);
        Assert.Contains(BuiltinRoleTypes.Producer, roleTypes);
    }

    [Fact]
    public async Task GetAgentStatusesAsync_AllAgentsStartIdle()
    {
        var service = CreateService();
        var projectId = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(projectId);
        var statuses = await service.GetAgentStatusesAsync(projectId);

        Assert.All(statuses, s => Assert.Equal(AgentStatus.Idle, s.Status));
    }

    [Fact]
    public async Task GetAgentStatusesAsync_UnknownProject_ReturnsEmptyList()
    {
        var service = CreateService();

        var statuses = await service.GetAgentStatusesAsync(Guid.NewGuid());

        Assert.Empty(statuses);
    }

    [Fact]
    public async Task RouteToAgentAsync_UnregisteredRole_ReturnsError()
    {
        var factory = CreateFactoryWithRoles(BuiltinRoleTypes.Communicator);
        var service = CreateService(factory);
        var projectId = Guid.NewGuid();

        var result = await service.RouteToAgentAsync(projectId, "nonexistent_role",
            new AgentMessage { Content = "test" });

        Assert.False(result.Success);
        Assert.Contains("nonexistent_role", result.Content);
    }

    [Fact]
    public async Task InitializeProjectAgentsAsync_ExcludesOrchestrator()
    {
        var factory = CreateFactoryWithRoles(
            BuiltinRoleTypes.Orchestrator,
            BuiltinRoleTypes.Communicator);
        var service = CreateService(factory);
        var projectId = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(projectId);
        var statuses = await service.GetAgentStatusesAsync(projectId);

        var roleTypes = statuses.Select(s => s.RoleType).ToList();
        // Orchestrator is excluded from batch initialization
        Assert.DoesNotContain(BuiltinRoleTypes.Orchestrator, roleTypes);
        Assert.Contains(BuiltinRoleTypes.Communicator, roleTypes);
    }

    [Fact]
    public async Task InitializeProjectAgentsAsync_MultipleProjects_Independent()
    {
        var service = CreateService();
        var project1 = Guid.NewGuid();
        var project2 = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(project1);

        var statuses1 = await service.GetAgentStatusesAsync(project1);
        var statuses2 = await service.GetAgentStatusesAsync(project2);

        Assert.True(statuses1.Count > 0);
        Assert.Empty(statuses2);
    }

    [Fact]
    public async Task GetAgentStatusesAsync_ReturnsRoleNames()
    {
        var factory = CreateFactoryWithRoles(BuiltinRoleTypes.Communicator);
        var service = CreateService(factory);
        var projectId = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(projectId);
        var statuses = await service.GetAgentStatusesAsync(projectId);

        Assert.All(statuses, s =>
        {
            Assert.False(string.IsNullOrEmpty(s.RoleName));
            Assert.False(string.IsNullOrEmpty(s.RoleType));
        });
    }
}
