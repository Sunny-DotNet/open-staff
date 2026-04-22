using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Application.Agents.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Plugin.GitHubCopilot;

namespace OpenStaff.Tests.Unit;

public sealed class GitHubCopilotTraceBridgeTests
{
    [Fact]
    public void ProjectUpdate_ShouldTranslateReasoningAndToolEvents()
    {
        var state = new TraceProjectionState();
        var timestamp = DateTimeOffset.UtcNow;

        var textUpdate = GitHubCopilotTraceAgent.ProjectUpdate(
            new AgentResponseUpdate(ChatRole.Assistant, [new TextContent("hello")
            {
                RawRepresentation = new AssistantMessageDeltaEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = timestamp,
                    Data = new AssistantMessageDeltaData
                    {
                        MessageId = "message-1",
                        DeltaContent = "hello"
                    }
                }
            }]),
            state);

        var reasoningUpdate = GitHubCopilotTraceAgent.ProjectUpdate(
            CreateRawUpdate(new AssistantReasoningDeltaEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp.AddMilliseconds(1),
                Data = new AssistantReasoningDeltaData
                {
                    ReasoningId = "reasoning-1",
                    DeltaContent = "thinking..."
                }
            }),
            state);

        var toolCallUpdate = GitHubCopilotTraceAgent.ProjectUpdate(
            CreateRawUpdate(new ToolExecutionStartEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp.AddMilliseconds(2),
                Data = new ToolExecutionStartData
                {
                    ToolCallId = "tool-1",
                    ToolName = "filesystem",
                    Arguments = new Dictionary<string, object?>
                    {
                        ["path"] = "A:\\repo"
                    }
                }
            }),
            state);

        var toolResultUpdate = GitHubCopilotTraceAgent.ProjectUpdate(
            CreateRawUpdate(new ToolExecutionCompleteEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp.AddMilliseconds(3),
                Data = new ToolExecutionCompleteData
                {
                    ToolCallId = "tool-1",
                    Success = true,
                    Result = new ToolExecutionCompleteDataResult
                    {
                        Content = "ok",
                        DetailedContent = "done"
                    }
                }
            }),
            state);

        var text = Assert.IsType<TextContent>(Assert.Single(textUpdate!.Contents));
        Assert.Equal("hello", text.Text);

        var reasoning = Assert.IsType<TextReasoningContent>(Assert.Single(reasoningUpdate!.Contents));
        Assert.Equal("thinking...", reasoning.Text);

        var toolCall = Assert.IsType<FunctionCallContent>(Assert.Single(toolCallUpdate!.Contents));
        Assert.Equal("tool-1", toolCall.CallId);
        Assert.Equal("filesystem", toolCall.Name);
        Assert.Equal("A:\\repo", toolCall.Arguments["path"]);

        var toolResult = Assert.IsType<FunctionResultContent>(Assert.Single(toolResultUpdate!.Contents));
        Assert.Equal("tool-1", toolResult.CallId);
        Assert.Equal("done", toolResult.Result);
        Assert.Null(toolResult.Exception);
    }

    [Fact]
    public void ProjectUpdate_ShouldTranslateSkillInvocationIntoCompletedToolCall()
    {
        var state = new TraceProjectionState();
        var timestamp = DateTimeOffset.UtcNow;

        var update = GitHubCopilotTraceAgent.ProjectUpdate(
            CreateRawUpdate(new SkillInvokedEvent
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Timestamp = timestamp,
                Data = new SkillInvokedData
                {
                    Name = "maps",
                    Path = "A:\\skills\\maps\\SKILL.md",
                    Content = "# maps",
                    AllowedTools = ["web_search"],
                    PluginName = "openstaff"
                }
            }),
            state);

        Assert.NotNull(update);
        Assert.Collection(
            update!.Contents,
            content =>
            {
                var skillCall = Assert.IsType<FunctionCallContent>(content);
                Assert.Equal("skill-11111111111111111111111111111111", skillCall.CallId);
                Assert.Equal("skill.invoke", skillCall.Name);
                Assert.Equal("maps", skillCall.Arguments["name"]);
                Assert.Equal("A:\\skills\\maps\\SKILL.md", skillCall.Arguments["path"]);
            },
            content =>
            {
                var skillResult = Assert.IsType<FunctionResultContent>(content);
                Assert.Equal("skill-11111111111111111111111111111111", skillResult.CallId);
                Assert.Equal("Loaded skill 'maps' from 'A:\\skills\\maps\\SKILL.md'.", skillResult.Result);
                Assert.Null(skillResult.Exception);
            });
    }

    [Fact]
    public void ProjectUpdate_ShouldUseAssistantMessageFallback_WhenDeltasAreMissing()
    {
        var state = new TraceProjectionState();
        var update = GitHubCopilotTraceAgent.ProjectUpdate(
            CreateRawUpdate(new AssistantMessageEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Data = new AssistantMessageData
                {
                    MessageId = "message-2",
                    Content = "final answer",
                    ReasoningText = "final reasoning"
                }
            }),
            state);

        Assert.NotNull(update);
        Assert.Collection(
            update!.Contents,
            content =>
            {
                var text = Assert.IsType<TextContent>(content);
                Assert.Equal("final answer", text.Text);
            },
            content =>
            {
                var reasoning = Assert.IsType<TextReasoningContent>(content);
                Assert.Equal("final reasoning", reasoning.Text);
            });
    }

    [Fact]
    public void IsSkillEnabledForRuntime_ShouldMatchCanonicalSkillPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(GitHubCopilotTraceBridgeTests), Guid.NewGuid().ToString("N"));
        var discoveryRoot = Path.Combine(root, "skills");
        var allowedSkillDirectory = Path.Combine(discoveryRoot, "openstaff-marker-skill");
        var allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(allowedSkillDirectory)
        };

        Assert.True(GitHubCopilotTraceAgent.IsSkillEnabledForRuntime(
            allowedSkillDirectory,
            allowedDirectories,
            [Path.GetFullPath(discoveryRoot)]));

        Assert.True(GitHubCopilotTraceAgent.IsSkillEnabledForRuntime(
            Path.Combine(allowedSkillDirectory, "SKILL.md"),
            allowedDirectories,
            [Path.GetFullPath(discoveryRoot)]));
    }

    [Fact]
    public void IsSkillEnabledForRuntime_ShouldMapAgentStagedSkillPathsBackToDiscoveryRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(GitHubCopilotTraceBridgeTests), Guid.NewGuid().ToString("N"));
        var discoveryRoot = Path.Combine(root, "skills");
        var allowedSkillDirectory = Path.Combine(discoveryRoot, "openstaff-marker-skill");
        var stagedSkillDirectory = Path.Combine(root, ".agents", "skills", "openstaff-marker-skill");
        var allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(allowedSkillDirectory)
        };

        Assert.True(GitHubCopilotTraceAgent.IsSkillEnabledForRuntime(
            stagedSkillDirectory,
            allowedDirectories,
            [Path.GetFullPath(discoveryRoot)]));

        Assert.True(GitHubCopilotTraceAgent.IsSkillEnabledForRuntime(
            Path.Combine(stagedSkillDirectory, "SKILL.md"),
            allowedDirectories,
            [Path.GetFullPath(discoveryRoot)]));

        Assert.False(GitHubCopilotTraceAgent.IsSkillEnabledForRuntime(
            Path.Combine(root, ".agents", "skills", "different-skill", "SKILL.md"),
            allowedDirectories,
            [Path.GetFullPath(discoveryRoot)]));
    }

    [Fact]
    public async Task CreateExecutionSessionConfig_ShouldCarryStableSessionId_AndMergeRunOptionTools()
    {
        await using var client = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        var baseTool = AIFunctionFactory.Create((string input) => input, "base_tool");
        var runTool = AIFunctionFactory.Create((string input) => input, "run_tool");
        var baseConfig = new SessionConfig
        {
            Model = "claude-sonnet-4.5",
            ReasoningEffort = "low",
            SessionId = "base-session",
            Tools = [baseTool]
        };
        var innerAgent = new GitHubCopilotAgent(client, baseConfig, ownsClient: false, id: "agent-1", name: "Copilot");
        var host = new Mock<IGitHubCopilotClientHost>(MockBehavior.Strict);
        var traceAgent = new GitHubCopilotTraceAgent(innerAgent, client, host.Object, baseConfig);

        var config = traceAgent.CreateExecutionSessionConfig(
            "stable-session",
            new ChatClientAgentRunOptions(new ChatOptions
            {
                Tools = [runTool],
                Reasoning = new()
                {
                    Effort = ReasoningEffort.Medium
                }
            }));

        Assert.Equal("stable-session", config.SessionId);
        Assert.Equal("medium", config.ReasoningEffort);
        Assert.Equal(2, config.Tools!.Count);
    }

    [Fact]
    public async Task CreateExecutionSessionConfig_ShouldRespectConfiguredStreamingMode()
    {
        await using var client = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        var baseConfig = new SessionConfig
        {
            SessionId = "base-session",
            Streaming = false
        };
        var innerAgent = new GitHubCopilotAgent(client, baseConfig, ownsClient: false, id: "agent-streaming", name: "Copilot");
        var host = new Mock<IGitHubCopilotClientHost>(MockBehavior.Strict);
        var traceAgent = new GitHubCopilotTraceAgent(innerAgent, client, host.Object, baseConfig);

        var createConfig = traceAgent.CreateExecutionSessionConfig("stable-session", null);
        var resumeConfig = traceAgent.CreateExecutionResumeConfig(null);

        Assert.False(createConfig.Streaming);
        Assert.False(resumeConfig.Streaming);
    }

    [Fact]
    public async Task SessionManager_ShouldSeedStableSessionId_OnFirstCreate()
    {
        var sessionId = Guid.NewGuid();
        var agentInstanceId = Guid.NewGuid();
        var expectedCopilotSessionId = $"openstaff-session-{sessionId:N}-agent-{agentInstanceId:N}";

        var host = new Mock<IGitHubCopilotClientHost>(MockBehavior.Strict);
        host.Setup(item => item.SessionExistsAsync(expectedCopilotSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var registry = new Mock<IGitHubCopilotTemporarySessionRegistry>(MockBehavior.Strict);
        registry.Setup(item => item.RegisterAsync(expectedCopilotSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = new GitHubCopilotSessionManager(
            host.Object,
            registry.Object,
            NullLogger<GitHubCopilotSessionManager>.Instance);

        await using var client = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        var agent = new GitHubCopilotAgent(client, new SessionConfig(), ownsClient: false, id: "agent-2", name: "Copilot");

        var prepared = await manager.PrepareSessionAsync(agent, new AgentContext
        {
            SessionId = sessionId,
            AgentInstanceId = agentInstanceId,
            Scene = SceneType.Test
        });

        var typedSession = Assert.IsType<GitHubCopilotAgentSession>(prepared.Session);
        Assert.Equal(expectedCopilotSessionId, typedSession.SessionId);

        await prepared.ExecutionLease.DisposeAsync();
        registry.Verify(item => item.RegisterAsync(expectedCopilotSessionId, It.IsAny<CancellationToken>()), Times.Once);
        host.VerifyAll();
    }

    private static AgentResponseUpdate CreateRawUpdate(SessionEvent sessionEvent)
    {
        return new AgentResponseUpdate(ChatRole.Assistant, [new AIContent
        {
            RawRepresentation = sessionEvent
        }])
        {
            CreatedAt = sessionEvent.Timestamp
        };
    }
}

