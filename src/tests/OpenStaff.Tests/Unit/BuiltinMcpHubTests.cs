using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.Entities;
using OpenStaff.Mcp;
using OpenStaff.Mcp.Builtin;

namespace OpenStaff.Tests.Unit;

public sealed class BuiltinMcpHubTests
{
    [Fact]
    public async Task GetDraftToolsAsync_UsesBuiltinProvider_ForBuiltinTransport()
    {
        var provider = new FakeBuiltinMcpToolProvider();
        using var hub = new McpHub(
            NullLoggerFactory.Instance,
            builtinToolProviders: [provider]);

        var server = new McpServer
        {
            Id = Guid.NewGuid(),
            Name = "OpenStaff Shell",
            TransportType = McpTransportTypes.Builtin,
            Mode = McpServerModes.Local,
            Source = McpSources.Builtin,
            DefaultConfig = """{"transportType":"builtin","builtinProvider":"shell"}"""
        };

        var tools = await hub.GetDraftToolsAsync(server, selectedProfileId: null, parameterValues: null, CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal("shell_exec", tool.Name);
        Assert.Equal(1, provider.InvocationCount);
    }

    [Fact]
    public async Task GetToolsAsync_DoesNotReuseBuiltinToolsAcrossDifferentRuntimeConnections()
    {
        var provider = new FakeBuiltinMcpToolProvider();
        using var hub = new McpHub(
            NullLoggerFactory.Instance,
            builtinToolProviders: [provider]);

        var serverId = Guid.NewGuid();
        var firstConnection = new ResolvedMcpClientConnection(
            CacheKey: $"role:{Guid.NewGuid():N}:server:{serverId:N}",
            ServerId: serverId,
            Name: "OpenStaff Shell",
            TransportType: McpTransportTypes.Builtin,
            ConnectionConfigJson: """{"transportType":"builtin","builtinProvider":"shell"}""",
            EnvironmentVariables: null,
            SessionId: null);

        var secondSessionId = Guid.NewGuid();
        var secondConnection = firstConnection with
        {
            SessionId = secondSessionId
        };

        _ = await hub.GetToolsAsync(firstConnection, CancellationToken.None);
        _ = await hub.GetToolsAsync(secondConnection, CancellationToken.None);

        Assert.Equal(2, provider.InvocationCount);
        Assert.Equal([null, secondSessionId], provider.SeenSessionIds);
    }

    [Fact]
    public async Task WarmAsync_DoesNotCacheBuiltinToolsForLaterSessionReuse()
    {
        var provider = new FakeBuiltinMcpToolProvider();
        using var hub = new McpHub(
            NullLoggerFactory.Instance,
            builtinToolProviders: [provider]);

        var serverId = Guid.NewGuid();
        var warmConnection = new ResolvedMcpClientConnection(
            CacheKey: $"role:{Guid.NewGuid():N}:server:{serverId:N}",
            ServerId: serverId,
            Name: "OpenStaff Shell",
            TransportType: McpTransportTypes.Builtin,
            ConnectionConfigJson: """{"transportType":"builtin","builtinProvider":"shell"}""",
            EnvironmentVariables: null,
            SessionId: null);

        var runtimeSessionId = Guid.NewGuid();
        var runtimeConnection = warmConnection with
        {
            SessionId = runtimeSessionId
        };

        _ = await hub.WarmAsync(
            warmConnection,
            warmReason: "startup",
            pinClient: true,
            preloadToolSnapshot: true,
            CancellationToken.None);
        _ = await hub.GetToolsAsync(runtimeConnection, CancellationToken.None);

        Assert.Equal(2, provider.InvocationCount);
        Assert.Equal([null, runtimeSessionId], provider.SeenSessionIds);
    }

    private sealed class FakeBuiltinMcpToolProvider : IBuiltinMcpToolProvider
    {
        public string ProviderId => "shell";

        public int InvocationCount { get; private set; }

        public List<Guid?> SeenSessionIds { get; } = [];

        public Task<IReadOnlyList<McpRuntimeToolDescriptor>> GetToolsAsync(
            ResolvedMcpClientConnection connection,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            SeenSessionIds.Add(connection.SessionId);
            var tool = AIFunctionFactory.Create((string executable) => executable, "shell_exec", "fake shell tool", serializerOptions: null);
            return Task.FromResult<IReadOnlyList<McpRuntimeToolDescriptor>>([McpRuntimeToolDescriptor.FromAITool(tool)]);
        }
    }
}
