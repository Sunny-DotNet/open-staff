using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenStaff.Options;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public sealed class GitHubCopilotTemporarySessionRegistry : IGitHubCopilotTemporarySessionRegistry
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filename;

    public GitHubCopilotTemporarySessionRegistry(IOptions<OpenStaffOptions> openStaffOptions)
    {
        ArgumentNullException.ThrowIfNull(openStaffOptions);

        _filename = Path.Combine(
            openStaffOptions.Value.WorkingDirectory,
            "agents",
            "github-copilot",
            "temporary-sessions.json");
    }

    public async Task RegisterAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID is required.", nameof(sessionId));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessionIds = await ReadAsync(cancellationToken);
            if (sessionIds.Add(sessionId))
                await WriteAsync(sessionIds, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadAsync(cancellationToken))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID is required.", nameof(sessionId));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessionIds = await ReadAsync(cancellationToken);
            if (sessionIds.Remove(sessionId))
                await WriteAsync(sessionIds, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<HashSet<string>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filename))
            return new HashSet<string>(StringComparer.Ordinal);

        await using var stream = File.OpenRead(_filename);
        var payload = await JsonSerializer.DeserializeAsync<TemporarySessionsPayload>(stream, cancellationToken: cancellationToken)
            ?? new TemporarySessionsPayload();

        return new HashSet<string>(
            payload.SessionIds.Where(item => !string.IsNullOrWhiteSpace(item)),
            StringComparer.Ordinal);
    }

    private async Task WriteAsync(HashSet<string> sessionIds, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filename)
            ?? throw new InvalidOperationException("Temporary session registry path does not have a directory.");
        Directory.CreateDirectory(directory);

        if (sessionIds.Count == 0)
        {
            if (File.Exists(_filename))
                File.Delete(_filename);
            return;
        }

        await using var stream = File.Create(_filename);
        await JsonSerializer.SerializeAsync(
            stream,
            new TemporarySessionsPayload
            {
                SessionIds = sessionIds.OrderBy(item => item, StringComparer.Ordinal).ToList()
            },
            cancellationToken: cancellationToken);
    }

    private sealed class TemporarySessionsPayload
    {
        public List<string> SessionIds { get; set; } = [];
    }
}
