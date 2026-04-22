using GitHub.Copilot.SDK;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Vendor.GitHubCopilot;

namespace OpenStaff.Plugin.GitHubCopilot;

public sealed class GitHubCopilotClientHost : IGitHubCopilotClientHost, IHostedService, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IGitHubCopilotTemporarySessionRegistry _temporarySessionRegistry;
    private readonly ILogger<GitHubCopilotClientHost> _logger;
    private CopilotClient? _client;
    private bool _stopped;
    private bool _disposed;

    public GitHubCopilotClientHost(
        IGitHubCopilotTemporarySessionRegistry temporarySessionRegistry,
        ILogger<GitHubCopilotClientHost> logger)
    {
        _temporarySessionRegistry = temporarySessionRegistry;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CleanupTemporarySessionsAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopped || _disposed)
            return;

        await CleanupTemporarySessionsAsync(cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_stopped || _disposed)
                return;

            if (_client == null)
            {
                _stopped = true;
                return;
            }

            await DisposeClientAsync(_client);
            _client = null;
            _stopped = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await StopAsync(CancellationToken.None);
        _disposed = true;
        _gate.Dispose();
    }

    public async Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_client is { State: ConnectionState.Connected })
                return _client;

            if (_client != null)
            {
                await DisposeClientAsync(_client);
                _client = null;
            }

            var client = new CopilotClient(new CopilotClientOptions
            {
                UseLoggedInUser = true
            });

            await client.StartAsync(cancellationToken);
            _client = client;
            _logger.LogInformation("Started shared GitHub Copilot client.");
            return client;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID is required.", nameof(sessionId));

        var client = await GetClientAsync(cancellationToken);
        return await client.GetSessionMetadataAsync(sessionId, cancellationToken) != null;
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID is required.", nameof(sessionId));

        var client = await GetClientAsync(cancellationToken);
        await client.DeleteSessionAsync(sessionId, cancellationToken);
    }

    private async Task CleanupTemporarySessionsAsync(CancellationToken cancellationToken)
    {
        var sessionIds = await _temporarySessionRegistry.ListAsync(cancellationToken);
        if (sessionIds.Count == 0)
            return;

        _logger.LogInformation(
            "Cleaning up {Count} temporary GitHub Copilot sessions.",
            sessionIds.Count);

        foreach (var sessionId in sessionIds)
        {
            try
            {
                await DeleteSessionAsync(sessionId, cancellationToken);
                await _temporarySessionRegistry.RemoveAsync(sessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete temporary GitHub Copilot session {SessionId}; the cleanup journal entry was retained.",
                    sessionId);
            }
        }
    }

    private static async Task DisposeClientAsync(CopilotClient client)
    {
        try
        {
            await client.StopAsync();
        }
        finally
        {
            await client.DisposeAsync();
        }
    }
}
