namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public interface IGitHubCopilotTemporarySessionRegistry
{
    Task RegisterAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default);
}
