using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenStaff.Entities;
using OpenStaff.Mcp.Builtin;
using OpenStaff.Mcp.Services;

namespace OpenStaff.Mcp;

/// <summary>
/// Single MCP runtime entrypoint for client reuse, tool snapshots, warmup, invalidation, and cleanup.
/// </summary>
public sealed class McpHub : IDisposable
{
    private readonly ConcurrentDictionary<string, McpClientEntry> _clients = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _clientLocks = new();
    private readonly ConcurrentDictionary<string, McpToolSnapshotEntry> _toolSnapshots = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _toolSnapshotLocks = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpHub> _logger;
    private readonly Timer _cleanupTimer;
    private readonly McpProfileConnectionRenderer _profileConnectionRenderer;
    private readonly TimeSpan _lazyClientIdleTimeout;
    private readonly IReadOnlyDictionary<string, IBuiltinMcpToolProvider> _builtinToolProviders;

    public McpHub(
        ILoggerFactory loggerFactory,
        McpProfileConnectionRenderer? profileConnectionRenderer = null,
        IOptions<OpenStaffMcpOptions>? mcpOptions = null,
        IEnumerable<IBuiltinMcpToolProvider>? builtinToolProviders = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpHub>();
        _profileConnectionRenderer = profileConnectionRenderer ?? new McpProfileConnectionRenderer(new McpStructuredMetadataFactory());
        var configuredIdleTimeoutSeconds = mcpOptions?.Value.LazyClientIdleTimeoutSeconds ?? 300;
        _lazyClientIdleTimeout = TimeSpan.FromSeconds(configuredIdleTimeoutSeconds > 0 ? configuredIdleTimeoutSeconds : 300);
        _builtinToolProviders = (builtinToolProviders ?? [])
            .GroupBy(provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        _cleanupTimer = new Timer(CleanupIdleClients, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Task<IReadOnlyList<McpRuntimeToolDescriptor>> GetToolsAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken ct = default)
    {
        if (IsBuiltinTransport(connection.TransportType))
            return LoadBuiltinToolsAsync(connection, ct);

        return GetOrLoadToolSnapshotAsync(
            connection.CacheKey,
            tools => new McpToolSnapshotEntry(
                connection.CacheKey,
                tools,
                serverId: connection.ServerId,
                agentRoleId: connection.AgentRoleId,
                projectId: connection.ProjectId),
            innerCt => LoadToolsFromClientAsync(GetClientAsync(connection, innerCt), innerCt),
            ct);
    }

    public async Task<IReadOnlyList<McpRuntimeToolDescriptor>> WarmAsync(
        ResolvedMcpClientConnection connection,
        string warmReason,
        bool pinClient,
        bool preloadToolSnapshot,
        CancellationToken ct = default)
    {
        if (IsBuiltinTransport(connection.TransportType))
        {
            _logger.LogDebug(
                "Warm request for builtin MCP connection {CacheKey} with reason {WarmReason}",
                connection.CacheKey,
                warmReason);
            return preloadToolSnapshot
                ? await LoadBuiltinToolsAsync(connection, ct)
                : [];
        }

        var client = await GetClientAsync(connection, ct);
        if (_clients.TryGetValue(connection.CacheKey, out var entry))
        {
            entry.Touch();
            if (pinClient)
                entry.MarkWarm(warmReason);
        }

        if (!preloadToolSnapshot)
        {
            return _toolSnapshots.TryGetValue(connection.CacheKey, out var cached)
                ? cached.Tools
                : [];
        }

        return await GetOrLoadToolSnapshotAsync(
            connection.CacheKey,
            tools => new McpToolSnapshotEntry(
                connection.CacheKey,
                tools,
                serverId: connection.ServerId,
                agentRoleId: connection.AgentRoleId,
                projectId: connection.ProjectId),
            innerCt => LoadToolsFromClientAsync(client, innerCt),
            ct);
    }

    public async Task<IReadOnlyList<McpRuntimeToolDescriptor>> GetDraftToolsAsync(
        McpServer server,
        string? selectedProfileId,
        string? parameterValues,
        CancellationToken ct = default)
    {
        var connection = BuildDraftConnection(server, selectedProfileId, parameterValues);
        if (IsBuiltinTransport(connection.TransportType))
            return await LoadBuiltinToolsAsync(connection, ct);

        await using var client = await CreateClientAsync(connection, ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return [.. tools.Select(McpRuntimeToolDescriptor.FromMcpClientTool)];
    }

    public Task<string?> GetPreloadSkipReasonAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken ct = default)
    {
        if (IsBuiltinTransport(connection.TransportType))
            return GetBuiltinPreloadSkipReasonAsync(connection, ct);

        return GetPreloadSkipReasonAsync(
            new McpServer
            {
                Id = connection.ServerId,
                Name = connection.Name,
                NpmPackage = connection.NpmPackage
            },
            new BindingConnectionParts(
                connection.Name,
                connection.TransportType,
                connection.ConnectionConfigJson,
                connection.EnvironmentVariables),
            ct);
    }

    public Task InvalidateAgentRoleAsync(Guid agentRoleId)
        => InvalidateAsync(
            client => client.AgentRoleId == agentRoleId,
            snapshot => snapshot.AgentRoleId == agentRoleId,
            $"agent-role:{agentRoleId:N}");

    public Task InvalidateProjectAsync(Guid projectId)
        => InvalidateAsync(
            client => client.ProjectId == projectId,
            snapshot => snapshot.ProjectId == projectId,
            $"project:{projectId:N}");

    public Task InvalidateServerAsync(Guid serverId)
        => InvalidateAsync(
            client => client.ServerId == serverId,
            snapshot => snapshot.ServerId == serverId,
            $"server:{serverId:N}");

    public async Task<McpClient> GetClientAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken ct = default)
    {
        if (IsBuiltinTransport(connection.TransportType))
            throw new InvalidOperationException("Builtin MCP connections do not expose external MCP clients.");

        if (_clients.TryGetValue(connection.CacheKey, out var entry) && !entry.IsDisposed)
        {
            entry.Touch();
            return entry.Client;
        }

        var gate = _clientLocks.GetOrAdd(connection.CacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_clients.TryGetValue(connection.CacheKey, out entry) && !entry.IsDisposed)
            {
                entry.Touch();
                return entry.Client;
            }

            var client = await CreateClientAsync(connection, ct);
            _clients[connection.CacheKey] = new McpClientEntry(
                client,
                connection.CacheKey,
                serverId: connection.ServerId,
                agentRoleId: connection.AgentRoleId,
                projectId: connection.ProjectId);
            _logger.LogInformation(
                "MCP client created for {CacheKey} using server {ServerId} ({Name})",
                connection.CacheKey,
                connection.ServerId,
                connection.Name);
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    private static Task<IReadOnlyList<McpRuntimeToolDescriptor>> LoadToolsFromClientAsync(
        Task<McpClient> clientTask,
        CancellationToken ct)
        => LoadToolsFromResolvedClientAsync(clientTask, ct);

    private static Task<IReadOnlyList<McpRuntimeToolDescriptor>> LoadToolsFromClientAsync(
        McpClient client,
        CancellationToken ct)
        => LoadToolsFromResolvedClientAsync(Task.FromResult(client), ct);

    private static async Task<IReadOnlyList<McpRuntimeToolDescriptor>> LoadToolsFromResolvedClientAsync(
        Task<McpClient> clientTask,
        CancellationToken ct)
    {
        var client = await clientTask;
        return [.. (await client.ListToolsAsync(cancellationToken: ct)).Select(McpRuntimeToolDescriptor.FromMcpClientTool)];
    }

    private Task<IReadOnlyList<McpRuntimeToolDescriptor>> LoadBuiltinToolsAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken ct)
        => GetBuiltinToolProvider(connection).GetToolsAsync(connection, ct);

    private async Task<McpClient> CreateClientAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken ct)
    {
        if (IsBuiltinTransport(connection.TransportType))
            throw new InvalidOperationException("Builtin MCP connections do not create external transports.");

        var connectionConfig = !string.IsNullOrWhiteSpace(connection.ConnectionConfigJson)
            ? JsonSerializer.Deserialize<ConnectionConfigModel>(
                connection.ConnectionConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : null;

        IClientTransport transport;
        if (IsHttpLikeTransport(connection.TransportType))
        {
            var url = connectionConfig?.Url ?? throw new InvalidOperationException("HTTP-like transport requires a URL");
            var headers = connectionConfig.Headers?
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            transport = McpHttpClientTransportFactory.Create(new HttpClientTransportOptions
            {
                Endpoint = new Uri(url),
                Name = connection.Name,
                TransportMode = ResolveHttpTransportMode(connection.TransportType),
                AdditionalHeaders = headers
            }, _loggerFactory);
        }
        else
        {
            var command = connectionConfig?.Command ?? throw new InvalidOperationException("Stdio transport requires a command");
            transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = command,
                Arguments = connectionConfig.Args ?? [],
                Name = connection.Name,
                EnvironmentVariables = connection.EnvironmentVariables?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                WorkingDirectory = connectionConfig.WorkingDirectory
            });
        }

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "OpenStaff",
                    Version = "1.0.0"
                }
            },
            loggerFactory: _loggerFactory,
            cancellationToken: ct);
    }

    private async Task<string?> GetPreloadSkipReasonAsync(
        McpServer server,
        BindingConnectionParts resolved,
        CancellationToken ct)
    {
        if (IsBuiltinTransport(resolved.TransportType))
            return await GetBuiltinPreloadSkipReasonAsync(
                new ResolvedMcpClientConnection(
                    CacheKey: $"preload:{server.Id:N}",
                    ServerId: server.Id,
                    Name: resolved.Name,
                    TransportType: resolved.TransportType,
                    ConnectionConfigJson: resolved.ConnectionConfigJson,
                    EnvironmentVariables: resolved.EnvironmentVariables,
                    NpmPackage: server.NpmPackage),
                ct);

        if (IsBraveSearchServer(server)
            && !HasConfiguredEnvironmentVariable(resolved.EnvironmentVariables, "BRAVE_API_KEY"))
        {
            return "BRAVE_API_KEY is not configured.";
        }

        if (!IsHttpLikeTransport(resolved.TransportType))
            return null;

        var connectionConfig = DeserializeConnectionConfig(resolved.ConnectionConfigJson);
        if (string.IsNullOrWhiteSpace(connectionConfig?.Url))
            return "Remote MCP URL is missing.";

        if (!Uri.TryCreate(connectionConfig.Url, UriKind.Absolute, out var uri))
            return $"Remote MCP URL '{connectionConfig.Url}' is invalid.";

        if (uri.IsLoopback || IPAddress.TryParse(uri.Host, out _))
            return null;

        if (!await CanResolveHostAsync(uri.Host, ct))
            return $"Remote MCP host '{uri.Host}' cannot be resolved.";

        return null;
    }

    private async Task<IReadOnlyList<McpRuntimeToolDescriptor>> GetOrLoadToolSnapshotAsync(
        string cacheKey,
        Func<IReadOnlyList<McpRuntimeToolDescriptor>, McpToolSnapshotEntry> snapshotFactory,
        Func<CancellationToken, Task<IReadOnlyList<McpRuntimeToolDescriptor>>> loader,
        CancellationToken ct)
    {
        if (_toolSnapshots.TryGetValue(cacheKey, out var cached))
            return cached.Tools;

        var gate = _toolSnapshotLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_toolSnapshots.TryGetValue(cacheKey, out cached))
                return cached.Tools;

            var tools = await loader(ct);
            _toolSnapshots[cacheKey] = snapshotFactory(tools);
            _logger.LogDebug("Cached MCP tool snapshot for {CacheKey} with {ToolCount} tools", cacheKey, tools.Count);
            return tools;
        }
        finally
        {
            gate.Release();
        }
    }

    private ResolvedMcpClientConnection BuildDraftConnection(
        McpServer server,
        string? selectedProfileId,
        string? parameterValues)
    {
        var rendered = _profileConnectionRenderer.RenderForConfig(server, selectedProfileId, parameterValues);
        return new ResolvedMcpClientConnection(
            CacheKey: $"draft:{server.Id:N}:{selectedProfileId ?? string.Empty}:{parameterValues ?? string.Empty}",
            ServerId: server.Id,
            Name: server.Name,
            TransportType: rendered["transportType"]?.GetValue<string>() ?? server.TransportType,
            ConnectionConfigJson: rendered.ToJsonString(),
            EnvironmentVariables: ExtractEnvironmentVariables(rendered),
            NpmPackage: server.NpmPackage);
    }

    private Task<string?> GetBuiltinPreloadSkipReasonAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken ct)
    {
        var provider = TryGetBuiltinToolProvider(connection);
        if (provider == null)
            return Task.FromResult<string?>($"Builtin MCP provider '{ResolveBuiltinProviderId(connection) ?? "unknown"}' is not registered.");

        return provider.GetPreloadSkipReasonAsync(connection, ct);
    }

    private IBuiltinMcpToolProvider GetBuiltinToolProvider(ResolvedMcpClientConnection connection)
        => TryGetBuiltinToolProvider(connection)
            ?? throw new InvalidOperationException(
                $"Builtin MCP provider '{ResolveBuiltinProviderId(connection) ?? "unknown"}' is not registered for '{connection.Name}'.");

    private IBuiltinMcpToolProvider? TryGetBuiltinToolProvider(ResolvedMcpClientConnection connection)
    {
        var providerId = ResolveBuiltinProviderId(connection);
        if (string.IsNullOrWhiteSpace(providerId))
            return null;

        return _builtinToolProviders.TryGetValue(providerId, out var provider)
            ? provider
            : null;
    }

    private static string? ResolveBuiltinProviderId(ResolvedMcpClientConnection connection)
        => DeserializeConnectionConfig(connection.ConnectionConfigJson)?.BuiltinProvider;

    private async Task InvalidateAsync(
        Func<McpClientEntry, bool> clientPredicate,
        Func<McpToolSnapshotEntry, bool> snapshotPredicate,
        string reason)
    {
        var snapshotKeys = _toolSnapshots
            .Where(item => snapshotPredicate(item.Value))
            .Select(item => item.Key)
            .ToList();
        foreach (var snapshotKey in snapshotKeys)
            _toolSnapshots.TryRemove(snapshotKey, out _);

        var clientKeys = _clients
            .Where(item => clientPredicate(item.Value))
            .Select(item => item.Key)
            .ToList();
        foreach (var clientKey in clientKeys)
        {
            if (_clients.TryRemove(clientKey, out var entry))
                await entry.DisposeAsync();
        }

        if (snapshotKeys.Count > 0 || clientKeys.Count > 0)
        {
            _logger.LogInformation(
                "Invalidated MCP caches for {Reason}: {SnapshotCount} tool snapshots, {ClientCount} clients",
                reason,
                snapshotKeys.Count,
                clientKeys.Count);
        }
    }

    private static ConnectionConfigModel? DeserializeConnectionConfig(string? connectionConfigJson)
    {
        if (string.IsNullOrWhiteSpace(connectionConfigJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ConnectionConfigModel>(
                connectionConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, string?>? ExtractEnvironmentVariables(JsonObject config)
    {
        var envNode = config["env"] as JsonObject ?? config["environmentVariables"] as JsonObject;
        if (envNode == null)
            return null;

        config.Remove("env");
        config.Remove("environmentVariables");

        return envNode
            .Where(item => item.Value != null)
            .ToDictionary(
                item => item.Key,
                item => item.Value?.GetValue<string>() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasConfiguredEnvironmentVariable(
        IReadOnlyDictionary<string, string?>? environmentVariables,
        string key)
        => environmentVariables != null
           && environmentVariables.TryGetValue(key, out var value)
           && !string.IsNullOrWhiteSpace(value);

    private static async Task<bool> CanResolveHostAsync(string host, CancellationToken ct)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host).WaitAsync(ct);
            return addresses.Length > 0;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private static bool IsHttpLikeTransport(string transportType)
        => !string.Equals(transportType, McpTransportTypes.Stdio, StringComparison.OrdinalIgnoreCase)
           && !IsBuiltinTransport(transportType);

    private static bool IsBuiltinTransport(string transportType)
        => string.Equals(transportType, McpTransportTypes.Builtin, StringComparison.OrdinalIgnoreCase);

    private static HttpTransportMode ResolveHttpTransportMode(string transportType)
    {
        if (string.Equals(transportType, McpTransportTypes.Sse, StringComparison.OrdinalIgnoreCase))
            return HttpTransportMode.Sse;
        if (string.Equals(transportType, McpTransportTypes.StreamableHttp, StringComparison.OrdinalIgnoreCase))
            return HttpTransportMode.StreamableHttp;
        return HttpTransportMode.AutoDetect;
    }

    private static bool IsBraveSearchServer(McpServer server)
        => string.Equals(server.Name, "Brave Search", StringComparison.OrdinalIgnoreCase)
           || string.Equals(server.NpmPackage, "@modelcontextprotocol/server-brave-search", StringComparison.OrdinalIgnoreCase);

    private void CleanupIdleClients(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var (cacheKey, entry) in _clients)
        {
            if (!entry.IsPinned && now - entry.LastUsed > _lazyClientIdleTimeout)
            {
                if (_clients.TryRemove(cacheKey, out var removed))
                {
                    _ = removed.DisposeAsync();
                    _logger.LogInformation("MCP client idle-released for key {CacheKey}", cacheKey);
                }
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        foreach (var (_, entry) in _clients)
            _ = entry.DisposeAsync();

        _clients.Clear();
        foreach (var (_, gate) in _clientLocks)
            gate.Dispose();

        _clientLocks.Clear();
        foreach (var (_, gate) in _toolSnapshotLocks)
            gate.Dispose();

        _toolSnapshotLocks.Clear();
        _toolSnapshots.Clear();
    }

    private sealed class McpClientEntry : IAsyncDisposable
    {
        public McpClientEntry(
            McpClient client,
            string cacheKey,
            Guid? serverId = null,
            Guid? agentRoleId = null,
            Guid? projectId = null)
        {
            Client = client;
            CacheKey = cacheKey;
            ServerId = serverId;
            AgentRoleId = agentRoleId;
            ProjectId = projectId;
        }

        public McpClient Client { get; }
        public string CacheKey { get; }
        public Guid? ServerId { get; }
        public Guid? AgentRoleId { get; }
        public Guid? ProjectId { get; }
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public bool IsPinned { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Touch() => LastUsed = DateTime.UtcNow;

        public void MarkWarm(string _)
        {
            IsPinned = true;
            Touch();
        }

        public async ValueTask DisposeAsync()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            await Client.DisposeAsync();
        }
    }

    private sealed class McpToolSnapshotEntry
    {
        public McpToolSnapshotEntry(
            string cacheKey,
            IReadOnlyList<McpRuntimeToolDescriptor> tools,
            Guid? serverId = null,
            Guid? agentRoleId = null,
            Guid? projectId = null)
        {
            CacheKey = cacheKey;
            Tools = tools;
            ServerId = serverId;
            AgentRoleId = agentRoleId;
            ProjectId = projectId;
        }

        public string CacheKey { get; }
        public IReadOnlyList<McpRuntimeToolDescriptor> Tools { get; }
        public Guid? ServerId { get; }
        public Guid? AgentRoleId { get; }
        public Guid? ProjectId { get; }
    }

    private sealed class ConnectionConfigModel
    {
        public string? Command { get; set; }
        public string[]? Args { get; set; }
        public string? Url { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? BuiltinProvider { get; set; }
    }

    private sealed record BindingConnectionParts(
        string Name,
        string TransportType,
        string? ConnectionConfigJson,
        IReadOnlyDictionary<string, string?>? EnvironmentVariables);
}
