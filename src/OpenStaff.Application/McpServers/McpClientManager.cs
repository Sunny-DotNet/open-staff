using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Application.McpServers;

/// <summary>
/// MCP Client 连接池管理器 — 按需连接，空闲超时释放
/// </summary>
public class McpClientManager : IDisposable
{
    private readonly ConcurrentDictionary<Guid, McpClientEntry> _clients = new();
    private readonly EncryptionService _encryption;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpClientManager> _logger;
    private readonly Timer _cleanupTimer;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    public McpClientManager(EncryptionService encryption, ILoggerFactory loggerFactory)
    {
        _encryption = encryption;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpClientManager>();
        _cleanupTimer = new Timer(CleanupIdleClients, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// 获取或创建 MCP Client 连接
    /// </summary>
    public async Task<McpClient> GetOrCreateAsync(McpServerConfig config, CancellationToken ct = default)
    {
        if (_clients.TryGetValue(config.Id, out var entry) && !entry.IsDisposed)
        {
            entry.LastUsed = DateTime.UtcNow;
            return entry.Client;
        }

        var client = await CreateClientAsync(config, ct);
        var newEntry = new McpClientEntry(client, config.Id);
        _clients[config.Id] = newEntry;
        _logger.LogInformation("MCP Client created for config {ConfigId} ({Name})", config.Id, config.Name);
        return client;
    }

    /// <summary>
    /// 列出 MCP Server 暴露的工具
    /// </summary>
    public async Task<IList<McpClientTool>> ListToolsAsync(McpServerConfig config, CancellationToken ct = default)
    {
        var client = await GetOrCreateAsync(config, ct);
        return await client.ListToolsAsync(cancellationToken: ct);
    }

    /// <summary>
    /// 释放指定配置的连接
    /// </summary>
    public async Task ReleaseAsync(Guid configId)
    {
        if (_clients.TryRemove(configId, out var entry))
        {
            await entry.DisposeAsync();
            _logger.LogInformation("MCP Client released for config {ConfigId}", configId);
        }
    }

    private async Task<McpClient> CreateClientAsync(McpServerConfig config, CancellationToken ct)
    {
        var connConfig = !string.IsNullOrEmpty(config.ConnectionConfig)
            ? JsonSerializer.Deserialize<ConnectionConfigModel>(config.ConnectionConfig)
            : null;

        // 解密环境变量
        Dictionary<string, string>? envVars = null;
        if (!string.IsNullOrEmpty(config.EnvironmentVariables))
        {
            try
            {
                var decrypted = _encryption.Decrypt(config.EnvironmentVariables);
                envVars = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt env vars for config {ConfigId}", config.Id);
            }
        }

        IClientTransport transport;

        if (config.TransportType == McpTransportTypes.Http)
        {
            var url = connConfig?.Url ?? throw new InvalidOperationException("HTTP transport requires a URL");
            transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(url),
                Name = config.Name
            });
        }
        else // stdio
        {
            var command = connConfig?.Command ?? throw new InvalidOperationException("Stdio transport requires a command");
            var args = connConfig.Args ?? [];

            transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = command,
                Arguments = args,
                Name = config.Name,
                EnvironmentVariables = envVars
            });
        }

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ClientInfo = new Implementation { Name = "OpenStaff", Version = "1.0.0" } },
            loggerFactory: _loggerFactory,
            cancellationToken: ct);
    }

    private void CleanupIdleClients(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var (configId, entry) in _clients)
        {
            if (now - entry.LastUsed > IdleTimeout)
            {
                if (_clients.TryRemove(configId, out var removed))
                {
                    _ = removed.DisposeAsync();
                    _logger.LogInformation("MCP Client idle-released for config {ConfigId}", configId);
                }
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        foreach (var (_, entry) in _clients)
        {
            _ = entry.DisposeAsync();
        }
        _clients.Clear();
    }

    private class McpClientEntry : IAsyncDisposable
    {
        public McpClient Client { get; }
        public Guid ConfigId { get; }
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public bool IsDisposed { get; private set; }

        public McpClientEntry(McpClient client, Guid configId)
        {
            Client = client;
            ConfigId = configId;
        }

        public async ValueTask DisposeAsync()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            await Client.DisposeAsync();
        }
    }

    private class ConnectionConfigModel
    {
        public string? Command { get; set; }
        public string[]? Args { get; set; }
        public string? Url { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}
