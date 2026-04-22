using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Core.Agents;

namespace OpenStaff.Plugin.GitHubCopilot;

public sealed class GitHubCopilotSessionManager : IGitHubCopilotSessionManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new(StringComparer.Ordinal);
    private readonly IGitHubCopilotClientHost _clientHost;
    private readonly IGitHubCopilotTemporarySessionRegistry _temporarySessionRegistry;
    private readonly ILogger<GitHubCopilotSessionManager> _logger;

    public GitHubCopilotSessionManager(
        IGitHubCopilotClientHost clientHost,
        IGitHubCopilotTemporarySessionRegistry temporarySessionRegistry,
        ILogger<GitHubCopilotSessionManager> logger)
    {
        _clientHost = clientHost;
        _temporarySessionRegistry = temporarySessionRegistry;
        _logger = logger;
    }

    public async Task<GitHubCopilotPreparedSession> PrepareSessionAsync(
        GitHubCopilotAgent agent,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(context);

        var conversationIdentity = GitHubCopilotConversationIdentity.Create(context);
        var sessionLock = _sessionLocks.GetOrAdd(
            conversationIdentity.CopilotSessionId,
            static _ => new SemaphoreSlim(1, 1));

        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            AgentSession session;
            if (await _clientHost.SessionExistsAsync(conversationIdentity.CopilotSessionId, cancellationToken))
            {
                _logger.LogDebug(
                    "Reusing GitHub Copilot session {SessionId}.",
                    conversationIdentity.CopilotSessionId);

                session = await agent.CreateSessionAsync(conversationIdentity.CopilotSessionId);
            }
            else
            {
                _logger.LogDebug(
                    "Creating GitHub Copilot session {SessionId}.",
                    conversationIdentity.CopilotSessionId);

                session = await agent.CreateSessionAsync(conversationIdentity.CopilotSessionId);
                if (conversationIdentity.IsTemporary)
                    await _temporarySessionRegistry.RegisterAsync(conversationIdentity.CopilotSessionId, cancellationToken);
            }

            return new GitHubCopilotPreparedSession(
                session,
                new SessionExecutionLease(sessionLock));
        }
        catch
        {
            sessionLock.Release();
            throw;
        }
    }

    private sealed class SessionExecutionLease : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sessionLock;
        private int _disposed;

        public SessionExecutionLease(SemaphoreSlim sessionLock)
        {
            _sessionLock = sessionLock;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _sessionLock.Release();

            return ValueTask.CompletedTask;
        }
    }
}
