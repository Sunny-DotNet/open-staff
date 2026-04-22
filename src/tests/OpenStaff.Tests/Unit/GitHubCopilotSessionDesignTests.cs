using Microsoft.Extensions.Options;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Core.Agents;
using OpenStaff.Options;

namespace OpenStaff.Tests.Unit;

public sealed class GitHubCopilotSessionDesignTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(
        Path.GetTempPath(),
        "OpenStaff.Tests",
        nameof(GitHubCopilotSessionDesignTests),
        Guid.NewGuid().ToString("N"));

    public GitHubCopilotSessionDesignTests()
    {
        Directory.CreateDirectory(_workingDirectory);
    }

    [Fact]
    public void ConversationIdentity_ShouldUseSessionAxis_ForTestChat()
    {
        var sessionId = Guid.NewGuid();
        var agentInstanceId = Guid.NewGuid();

        var identity = GitHubCopilotConversationIdentity.Create(new AgentContext
        {
            SessionId = sessionId,
            AgentInstanceId = agentInstanceId,
            Scene = SceneType.Test
        });

        Assert.True(identity.IsTemporary);
        Assert.Equal(
            $"openstaff-session-{sessionId:N}-agent-{agentInstanceId:N}",
            identity.CopilotSessionId);
    }

    [Fact]
    public void ConversationIdentity_ShouldUseProjectAxis_WhenSessionIsMissing()
    {
        var projectId = Guid.NewGuid();
        var agentInstanceId = Guid.NewGuid();

        var identity = GitHubCopilotConversationIdentity.Create(new AgentContext
        {
            ProjectId = projectId,
            AgentInstanceId = agentInstanceId,
            Scene = SceneType.ProjectGroup
        });

        Assert.False(identity.IsTemporary);
        Assert.Equal(
            $"openstaff-project-{projectId:N}-agent-{agentInstanceId:N}",
            identity.CopilotSessionId);
    }

    [Fact]
    public void ConversationIdentity_ShouldCreateTransientAxis_WhenNoStableAnchorExists()
    {
        var agentInstanceId = Guid.NewGuid();

        var identity = GitHubCopilotConversationIdentity.Create(new AgentContext
        {
            AgentInstanceId = agentInstanceId,
            Scene = SceneType.Private
        });

        Assert.True(identity.IsTemporary);
        Assert.StartsWith("openstaff-transient-", identity.CopilotSessionId, StringComparison.Ordinal);
        Assert.EndsWith($"-agent-{agentInstanceId:N}", identity.CopilotSessionId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TemporarySessionRegistry_ShouldPersistAndDeleteJournalFile()
    {
        var registry = new GitHubCopilotTemporarySessionRegistry(
            Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions
            {
                WorkingDirectory = _workingDirectory
            }));

        await registry.RegisterAsync("session-b");
        await registry.RegisterAsync("session-a");

        var journalPath = Path.Combine(
            _workingDirectory,
            "agents",
            "github-copilot",
            "temporary-sessions.json");

        Assert.True(File.Exists(journalPath));
        Assert.Equal(["session-a", "session-b"], await registry.ListAsync());

        await registry.RemoveAsync("session-a");
        Assert.Equal(["session-b"], await registry.ListAsync());

        await registry.RemoveAsync("session-b");
        Assert.False(File.Exists(journalPath));
        Assert.Empty(await registry.ListAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
            Directory.Delete(_workingDirectory, recursive: true);
    }
}
