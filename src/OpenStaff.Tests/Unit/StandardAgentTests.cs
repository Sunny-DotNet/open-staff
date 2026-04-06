using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agents;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Models;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class StandardAgentTests
{
    private static RoleConfig CreateConfig(string roleType = "communicator") => new()
    {
        RoleType = roleType,
        Name = "TestRole",
        SystemPrompt = "You are a test agent.",
        IsBuiltin = true,
        Tools = new List<string>(),
        ModelParameters = new ModelParameters { Temperature = 0.5, MaxTokens = 2048 }
    };

    private static StandardAgent CreateAgent(
        RoleConfig? config = null,
        IAgentToolRegistry? toolRegistry = null,
        AIAgentFactory? aiAgentFactory = null)
    {
        config ??= CreateConfig();
        toolRegistry ??= new Mock<IAgentToolRegistry>().Object;
        aiAgentFactory ??= new AIAgentFactory(new ChatClientFactory(new Mock<ILoggerFactory>().Object), new Mock<ILoggerFactory>().Object);
        var logger = new Mock<ILogger<StandardAgent>>().Object;
        return new StandardAgent(config, toolRegistry, aiAgentFactory, logger);
    }

    [Fact]
    public void Constructor_SetsRoleTypeFromConfig()
    {
        var agent = CreateAgent(config: CreateConfig("architect"));
        Assert.Equal("architect", agent.RoleType);
    }

    [Fact]
    public void Constructor_SetsRoleTypeFromConfig_Communicator()
    {
        var agent = CreateAgent(config: CreateConfig(BuiltinRoleTypes.Communicator));
        Assert.Equal(BuiltinRoleTypes.Communicator, agent.RoleType);
    }

    [Fact]
    public void Constructor_SetsRoleTypeFromConfig_DecisionMaker()
    {
        var agent = CreateAgent(config: CreateConfig(BuiltinRoleTypes.DecisionMaker));
        Assert.Equal(BuiltinRoleTypes.DecisionMaker, agent.RoleType);
    }

    [Fact]
    public void Status_DefaultsToIdle()
    {
        var agent = CreateAgent();
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    [Fact]
    public void Agent_ImplementsIAgent()
    {
        var agent = CreateAgent();
        Assert.IsAssignableFrom<IAgent>(agent);
    }

    [Fact]
    public async Task InitializeAsync_SetsContextAndKeepsIdle()
    {
        var agent = CreateAgent();
        var context = new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Test" },
            Project = new Project { Id = Guid.NewGuid() },
            NotificationService = new Mock<INotificationService>().Object,
            Language = "en"
        };

        await agent.InitializeAsync(context);

        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    [Fact]
    public async Task ProcessAsync_WithoutProvider_ReturnsError()
    {
        var config = CreateConfig("producer");
        config.SystemPrompt = "You are a producer.";

        var toolRegistryMock = new Mock<IAgentToolRegistry>();
        toolRegistryMock
            .Setup(t => t.GetTools(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<IAgentTool>());

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agent = CreateAgent(config, toolRegistryMock.Object);
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "producer", Name = "Producer" },
            Project = new Project { Id = Guid.NewGuid() },
            Account = null,
            NotificationService = notificationMock.Object,
            Language = "en"
        });

        var result = await agent.ProcessAsync(new AgentMessage { Content = "Build something" });

        Assert.False(result.Success);
        Assert.Contains("供应商", result.Content);
    }

    [Fact]
    public async Task ProcessAsync_StatusReturnsToIdleOrErrorAfterProcess()
    {
        var config = CreateConfig("communicator");
        config.SystemPrompt = "You are a communicator.";

        var toolRegistryMock = new Mock<IAgentToolRegistry>();
        toolRegistryMock.Setup(t => t.GetTools(It.IsAny<IEnumerable<string>>())).Returns(new List<IAgentTool>());

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(n => n.NotifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agent = CreateAgent(config, toolRegistryMock.Object);
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Comm" },
            Project = new Project { Id = Guid.NewGuid() },
            Account = null,
            NotificationService = notificationMock.Object,
            Language = "en"
        });

        var result = await agent.ProcessAsync(new AgentMessage { Content = "hello" });

        // Without provider, it returns error
        Assert.False(result.Success);
    }

    [Fact]
    public async Task StopAsync_SetsStatusToIdle()
    {
        var agent = CreateAgent();
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Comm" },
            Project = new Project { Id = Guid.NewGuid() },
            NotificationService = new Mock<INotificationService>().Object
        });

        await agent.StopAsync();

        Assert.Equal(AgentStatus.Idle, agent.Status);
    }
}
