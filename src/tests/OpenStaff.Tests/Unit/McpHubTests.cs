using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.Entities;
using OpenStaff.Mcp;

namespace OpenStaff.Tests.Unit;

public sealed class McpHubTests
{
    [Fact]
    public async Task GetPreloadSkipReasonAsync_ReturnsReason_ForMissingBraveApiKey()
    {
        using var manager = new McpHub(NullLoggerFactory.Instance);

        var reason = await manager.GetPreloadSkipReasonAsync(
            new ResolvedMcpClientConnection(
                CacheKey: "role:test",
                ServerId: Guid.NewGuid(),
                Name: "Brave Search",
                TransportType: McpTransportTypes.Stdio,
                ConnectionConfigJson: """{"command":"npx","args":["-y","@modelcontextprotocol/server-brave-search"]}""",
                EnvironmentVariables: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["BRAVE_API_KEY"] = ""
                },
                NpmPackage: "@modelcontextprotocol/server-brave-search"),
            CancellationToken.None);

        Assert.Equal("BRAVE_API_KEY is not configured.", reason);
    }

    [Fact]
    public async Task GetPreloadSkipReasonAsync_ReturnsReason_ForUnresolvableRemoteHost()
    {
        using var manager = new McpHub(NullLoggerFactory.Instance);

        var reason = await manager.GetPreloadSkipReasonAsync(
            new ResolvedMcpClientConnection(
                CacheKey: "project:test",
                ServerId: Guid.NewGuid(),
                Name: "GitHub",
                TransportType: McpTransportTypes.StreamableHttp,
                ConnectionConfigJson: """{"url":"https://nonexistent.openstaff.invalid/mcp"}""",
                EnvironmentVariables: null),
            CancellationToken.None);

        Assert.Contains("cannot be resolved", reason);
    }

    [Fact]
    public async Task ListToolsAsync_ThrowsWhenStructuredStdioDraftDoesNotProvideCommand()
    {
        using var manager = new McpHub(NullLoggerFactory.Instance);
        var server = new McpServer
        {
            Id = Guid.NewGuid(),
            Name = "Broken",
            TransportType = McpTransportTypes.Stdio,
            Mode = McpServerModes.Local,
            Source = McpSources.Custom,
            DefaultConfig = """{"schema":"openstaff.mcp-template.v1","profiles":[{"id":"stdio","profile_type":"advanced-legacy","transport_type":"stdio"}]}"""
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.GetDraftToolsAsync(server, selectedProfileId: "stdio", parameterValues: "{}", CancellationToken.None));

        Assert.Equal("Stdio transport requires a command", ex.Message);
    }

    [Fact]
    public void CleanupIdleClients_KeepsPinnedWarmClients()
    {
        using var manager = new McpHub(NullLoggerFactory.Instance);

        InsertClientEntry(
            manager,
            cacheKey: "role:warm",
            serverId: Guid.NewGuid(),
            lastUsed: DateTime.UtcNow.AddHours(-1),
            pinned: true,
            disposed: false);

        InvokeCleanupIdleClients(manager);

        Assert.Equal(1, GetClientCount(manager));
    }

    [Fact]
    public void CleanupIdleClients_RemovesExpiredLazyClients()
    {
        using var manager = new McpHub(NullLoggerFactory.Instance);

        InsertClientEntry(
            manager,
            cacheKey: "role:lazy",
            serverId: Guid.NewGuid(),
            lastUsed: DateTime.UtcNow.AddHours(-1),
            pinned: false,
            disposed: true);

        InvokeCleanupIdleClients(manager);

        Assert.Equal(0, GetClientCount(manager));
    }

    [Fact]
    public async Task InvalidateServerAsync_RemovesPinnedClients()
    {
        using var manager = new McpHub(NullLoggerFactory.Instance);
        var serverId = Guid.NewGuid();

        InsertClientEntry(
            manager,
            cacheKey: "role:pinned",
            serverId: serverId,
            lastUsed: DateTime.UtcNow,
            pinned: true,
            disposed: true);

        await manager.InvalidateServerAsync(serverId);

        Assert.Equal(0, GetClientCount(manager));
    }

    private static void InsertClientEntry(
        McpHub manager,
        string cacheKey,
        Guid serverId,
        DateTime lastUsed,
        bool pinned,
        bool disposed)
    {
        var entryType = typeof(McpHub).GetNestedType("McpClientEntry", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("McpClientEntry type not found.");
        var ctor = entryType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            [typeof(ModelContextProtocol.Client.McpClient), typeof(string), typeof(Guid?), typeof(Guid?), typeof(Guid?)],
            modifiers: null)
            ?? throw new InvalidOperationException("McpClientEntry constructor not found.");
        var entry = ctor.Invoke([null, cacheKey, serverId, null, null]);

        if (pinned)
        {
            var markWarm = entryType.GetMethod("MarkWarm", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("MarkWarm not found.");
            markWarm.Invoke(entry, ["test"]);
        }

        entryType.GetProperty("LastUsed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(entry, lastUsed);

        if (disposed)
        {
            entryType.GetField("<IsDisposed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(entry, true);
        }

        var clients = GetClientDictionary(manager);
        clients.GetType().GetProperty("Item")!.SetValue(clients, entry, [cacheKey]);
    }

    private static object GetClientDictionary(McpHub manager)
        => typeof(McpHub).GetField("_clients", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager)!
            ?? throw new InvalidOperationException("Client cache not found.");

    private static int GetClientCount(McpHub manager)
        => (int)(GetClientDictionary(manager).GetType().GetProperty("Count")!.GetValue(GetClientDictionary(manager))
            ?? 0);

    private static void InvokeCleanupIdleClients(McpHub manager)
        => typeof(McpHub).GetMethod("CleanupIdleClients", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, [null]);
}
