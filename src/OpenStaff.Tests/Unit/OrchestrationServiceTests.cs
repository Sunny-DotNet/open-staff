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
        var promptLoader = new OpenStaff.Agent.Builtin.Prompts.EmbeddedPromptLoader();
        return new BuiltinAgentProvider(services, toolRegistry, chatClientFactory, promptLoader, services.GetRequiredService<ILoggerFactory>());
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

        var result = await service.RouteToAgentAsync(projectId, "nonexistent_role", "test");

        Assert.False(result.Success);
    }
}
