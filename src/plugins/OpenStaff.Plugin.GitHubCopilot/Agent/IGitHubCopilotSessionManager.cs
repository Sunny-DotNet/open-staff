using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public interface IGitHubCopilotSessionManager
{
    Task<GitHubCopilotPreparedSession> PrepareSessionAsync(
        GitHubCopilotAgent agent,
        AgentContext context,
        CancellationToken cancellationToken = default);
}

public sealed record GitHubCopilotPreparedSession(
    AgentSession Session,
    IAsyncDisposable ExecutionLease);
