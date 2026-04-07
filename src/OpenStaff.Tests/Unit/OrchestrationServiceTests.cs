using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Agent.Tools;
using OpenStaff.Application.Orchestration;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class OrchestrationServiceTests
{
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

    private static AgentFactory CreateFactoryWithBuiltin()
    {
        return new AgentFactory(new IAgentProvider[] { CreateBuiltinProvider() });
    }

    private static OrchestrationService CreateService(AgentFactory? factory = null)
    {
        factory ??= CreateFactoryWithBuiltin();

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<OrchestrationService>>().Object;
        var providerResolverMock = new Mock<IProviderResolver>();

        return new OrchestrationService(factory, providerResolverMock.Object, notificationMock.Object, logger);
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
        Assert.True(statuses.Count > 0);
    }

    [Fact]
    public async Task GetAgentStatusesAsync_UnknownProject_ReturnsEmptyList()
    {
        var service = CreateService();

        var statuses = await service.GetAgentStatusesAsync(Guid.NewGuid());

        Assert.Empty(statuses);
    }

    [Fact]
    public async Task RouteToAgentAsync_UnregisteredProvider_ReturnsError()
    {
        var factory = new AgentFactory(Array.Empty<IAgentProvider>());
        var service = CreateService(factory);
        var projectId = Guid.NewGuid();

        var result = await service.RouteToAgentAsync(projectId, "nonexistent_role",
            new AgentMessage { Content = "test" });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task InitializeProjectAgentsAsync_ExcludesSecretary()
    {
        var service = CreateService();
        var projectId = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(projectId);
        var statuses = await service.GetAgentStatusesAsync(projectId);

        var roleTypes = statuses.Select(s => s.RoleType).ToList();
        Assert.DoesNotContain(BuiltinRoleTypes.Secretary, roleTypes);
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
    public async Task GetAgentStatusesAsync_AllAgentsStartIdle()
    {
        var service = CreateService();
        var projectId = Guid.NewGuid();

        await service.InitializeProjectAgentsAsync(projectId);
        var statuses = await service.GetAgentStatusesAsync(projectId);

        Assert.All(statuses, s => Assert.Equal(AgentStatus.Idle, s.Status));
    }

    [Fact]
    public async Task GetAgentStatusesAsync_ReturnsRoleNames()
    {
        var service = CreateService();
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
