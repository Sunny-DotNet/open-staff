using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenStaff.Mcp;
using OpenStaff.Repositories;

namespace OpenStaff.Application.McpServers.Services;

/// <summary>
/// 在宿主启动时预加载已启用 MCP 绑定的工具快照，降低首轮对话的冷启动成本。
/// Preloads tool snapshots for enabled MCP bindings during host startup so the first conversation avoids repeated ListTools calls.
/// </summary>
public sealed class McpToolSnapshotPreloadService : BackgroundService
{
    private readonly McpWarmupCoordinator _warmupCoordinator;
    private readonly OpenStaffMcpOptions _options;
    private readonly ILogger<McpToolSnapshotPreloadService> _logger;

    public McpToolSnapshotPreloadService(
        McpWarmupCoordinator warmupCoordinator,
        IOptions<OpenStaffMcpOptions> options,
        ILogger<McpToolSnapshotPreloadService> logger)
    {
        _warmupCoordinator = warmupCoordinator;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await PreloadAsync(stoppingToken);
    }

    public async Task PreloadAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableStartupWarmup)
        {
            _logger.LogInformation("Skipped MCP startup warmup because it is disabled in OpenStaff:Mcp.");
            return;
        }

        await _warmupCoordinator.WarmStartupConnectionsAsync(cancellationToken);
    }
}
