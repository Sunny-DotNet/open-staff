using GitHub.Copilot.SDK;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public interface IGitHubCopilotClientHost
{
    Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default);

    Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
