using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agents;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Events;
using OpenStaff.Core.Models;
using OpenStaff.Core.Services;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class StandardAgentTests
{
    private static RoleConfig CreateConfig(string roleType = "communicator") => new()
    {
        RoleType = roleType,
        Name = "TestRole",
        SystemPrompt = $"{roleType}.system",
        IsBuiltin = true,
        Tools = new List<string>(),
        ModelParameters = new ModelParameters { Temperature = 0.5, MaxTokens = 2048 }
    };

    private static StandardAgent CreateAgent(
        RoleConfig? config = null,
        IAgentToolRegistry? toolRegistry = null,
        IPromptLoader? promptLoader = null)
    {
        config ??= CreateConfig();
        toolRegistry ??= new Mock<IAgentToolRegistry>().Object;
        promptLoader ??= new Mock<IPromptLoader>().Object;
        var logger = new Mock<ILogger<StandardAgent>>().Object;
        return new StandardAgent(config, toolRegistry, promptLoader, logger);
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
            ModelClient = new Mock<IModelClient>().Object,
            EventPublisher = new Mock<IEventPublisher>().Object,
            Language = "en"
        };

        await agent.InitializeAsync(context);

        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    [Fact]
    public async Task ProcessAsync_CallsPromptLoaderWithConfiguredPromptName()
    {
        var config = CreateConfig("producer");
        var promptLoaderMock = new Mock<IPromptLoader>();
        promptLoaderMock
            .Setup(p => p.Load(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("You are a producer.");

        var modelClientMock = new Mock<IModelClient>();
        modelClientMock
            .Setup(m => m.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse { Content = "Done" });

        var toolRegistryMock = new Mock<IAgentToolRegistry>();
        toolRegistryMock
            .Setup(t => t.GetTools(It.IsAny<IEnumerable<string>>()))
            .Returns(new List<IAgentTool>());

        var agent = CreateAgent(config, toolRegistryMock.Object, promptLoaderMock.Object);
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "producer", Name = "Producer" },
            Project = new Project { Id = Guid.NewGuid() },
            ModelClient = modelClientMock.Object,
            EventPublisher = new Mock<IEventPublisher>().Object,
            Language = "en"
        });

        await agent.ProcessAsync(new AgentMessage { Content = "Build something" });

        promptLoaderMock.Verify(p => p.Load("producer.system", "en"), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsSuccessFromModelClient()
    {
        var config = CreateConfig("communicator");
        var promptLoaderMock = new Mock<IPromptLoader>();
        promptLoaderMock.Setup(p => p.Load(It.IsAny<string>(), It.IsAny<string>())).Returns("System prompt");

        var modelClientMock = new Mock<IModelClient>();
        modelClientMock
            .Setup(m => m.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse { Content = "Hello user!" });

        var toolRegistryMock = new Mock<IAgentToolRegistry>();
        toolRegistryMock.Setup(t => t.GetTools(It.IsAny<IEnumerable<string>>())).Returns(new List<IAgentTool>());

        var agent = CreateAgent(config, toolRegistryMock.Object, promptLoaderMock.Object);
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Communicator" },
            Project = new Project { Id = Guid.NewGuid() },
            ModelClient = modelClientMock.Object,
            EventPublisher = new Mock<IEventPublisher>().Object,
            Language = "zh-CN"
        });

        var result = await agent.ProcessAsync(new AgentMessage { Content = "Hi" });

        Assert.True(result.Success);
        Assert.Equal("Hello user!", result.Content);
    }

    [Fact]
    public async Task ProcessAsync_StatusReturnsToIdleAfterSuccess()
    {
        var config = CreateConfig("communicator");
        var promptLoaderMock = new Mock<IPromptLoader>();
        promptLoaderMock.Setup(p => p.Load(It.IsAny<string>(), It.IsAny<string>())).Returns("prompt");

        var modelClientMock = new Mock<IModelClient>();
        modelClientMock
            .Setup(m => m.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse { Content = "result" });

        var toolRegistryMock = new Mock<IAgentToolRegistry>();
        toolRegistryMock.Setup(t => t.GetTools(It.IsAny<IEnumerable<string>>())).Returns(new List<IAgentTool>());

        var agent = CreateAgent(config, toolRegistryMock.Object, promptLoaderMock.Object);
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Comm" },
            Project = new Project { Id = Guid.NewGuid() },
            ModelClient = modelClientMock.Object,
            EventPublisher = new Mock<IEventPublisher>().Object,
            Language = "en"
        });

        await agent.ProcessAsync(new AgentMessage { Content = "hello" });

        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    [Fact]
    public async Task ProcessAsync_WithoutModelClient_ReturnsError()
    {
        var agent = CreateAgent();
        // Initialize with null-like context that has no ModelClient
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Comm" },
            Project = new Project { Id = Guid.NewGuid() },
            ModelClient = null!,
            EventPublisher = new Mock<IEventPublisher>().Object,
            Language = "en"
        });

        var result = await agent.ProcessAsync(new AgentMessage { Content = "hello" });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ProcessAsync_SetsRoutingFromMarkers()
    {
        var config = CreateConfig("communicator");
        config.Routing = new RoutingConfig
        {
            Markers = new Dictionary<string, string>
            {
                ["REQUIREMENTS_COMPLETE"] = "decision_maker"
            }
        };

        var promptLoaderMock = new Mock<IPromptLoader>();
        promptLoaderMock.Setup(p => p.Load(It.IsAny<string>(), It.IsAny<string>())).Returns("prompt");

        var modelClientMock = new Mock<IModelClient>();
        modelClientMock
            .Setup(m => m.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse { Content = "All done [REQUIREMENTS_COMPLETE]" });

        var toolRegistryMock = new Mock<IAgentToolRegistry>();
        toolRegistryMock.Setup(t => t.GetTools(It.IsAny<IEnumerable<string>>())).Returns(new List<IAgentTool>());

        var agent = CreateAgent(config, toolRegistryMock.Object, promptLoaderMock.Object);
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Comm" },
            Project = new Project { Id = Guid.NewGuid() },
            ModelClient = modelClientMock.Object,
            EventPublisher = new Mock<IEventPublisher>().Object,
            Language = "en"
        });

        var result = await agent.ProcessAsync(new AgentMessage { Content = "requirements done" });

        Assert.Equal("decision_maker", result.TargetRole);
    }

    [Fact]
    public async Task ProcessAsync_SetsDefaultNextWhenNoMarkerMatched()
    {
        var config = CreateConfig("communicator");
        config.Routing = new RoutingConfig
        {
            Markers = new Dictionary<string, string>
            {
                ["REQUIREMENTS_COMPLETE"] = "decision_maker"
            },
            DefaultNext = "architect"
        };

        var promptLoaderMock = new Mock<IPromptLoader>();
        promptLoaderMock.Setup(p => p.Load(It.IsAny<string>(), It.IsAny<string>())).Returns("prompt");

        var modelClientMock = new Mock<IModelClient>();
        modelClientMock
            .Setup(m => m.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse { Content = "No marker here" });

        var toolRegistryMock = new Mock<IAgentToolRegistry>();
        toolRegistryMock.Setup(t => t.GetTools(It.IsAny<IEnumerable<string>>())).Returns(new List<IAgentTool>());

        var agent = CreateAgent(config, toolRegistryMock.Object, promptLoaderMock.Object);
        await agent.InitializeAsync(new AgentContext
        {
            ProjectId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole { RoleType = "communicator", Name = "Comm" },
            Project = new Project { Id = Guid.NewGuid() },
            ModelClient = modelClientMock.Object,
            EventPublisher = new Mock<IEventPublisher>().Object,
            Language = "en"
        });

        var result = await agent.ProcessAsync(new AgentMessage { Content = "hello" });

        Assert.Equal("architect", result.TargetRole);
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
            ModelClient = new Mock<IModelClient>().Object,
            EventPublisher = new Mock<IEventPublisher>().Object
        });

        await agent.StopAsync();

        Assert.Equal(AgentStatus.Idle, agent.Status);
    }
}
