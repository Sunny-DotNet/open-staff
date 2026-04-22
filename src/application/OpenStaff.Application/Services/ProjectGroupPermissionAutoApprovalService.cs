using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent.Services;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.Application.Settings.Services;
using OpenStaff.Entities;

namespace OpenStaff.Application.Services;

internal sealed class ProjectGroupPermissionAutoApprovalService : IHostedService
{
    private static readonly HashSet<string> AutoApprovedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "shell",
        "mcp"
    };

    private readonly IPermissionRequestHandler _permissionRequestHandler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectGroupPermissionAutoApprovalService> _logger;
    private IDisposable? _registration;

    public ProjectGroupPermissionAutoApprovalService(
        IPermissionRequestHandler permissionRequestHandler,
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectGroupPermissionAutoApprovalService> logger)
    {
        _permissionRequestHandler = permissionRequestHandler;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _registration = _permissionRequestHandler.Register(HandleAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registration?.Dispose();
        _registration = null;
        return Task.CompletedTask;
    }

    private async Task<PermissionAuthorizationResult?> HandleAsync(
        PermissionAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ShouldAttemptAutoApproval(request))
            return null;

        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
        var settings = await settingsService.GetAllSettingsAsync(cancellationToken).ConfigureAwait(false);
        var autoApproveEnabled = settings
            .FirstOrDefault(item => string.Equals(item.Key, SystemSettingsKeys.ProjectGroupAutoApproveCapabilities, StringComparison.Ordinal))
            ?.Value;
        if (!bool.TryParse(autoApproveEnabled, out var enabled) || !enabled)
            return null;

        _logger.LogInformation(
            "Auto-approved project-group permission request {RequestId} for kind {Kind} (tool: {ToolName}).",
            request.RequestId,
            request.Kind,
            request.ToolName);

        return new PermissionAuthorizationResult(
            PermissionAuthorizationKind.Accept,
            PermissionAuthorizationSource.InteractiveListener,
            ListenerId: "project-group-auto-approval");
    }

    private static bool ShouldAttemptAutoApproval(PermissionAuthorizationRequest request)
    {
        if (!string.Equals(request.Scene, SessionSceneTypes.ProjectGroup, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!AutoApprovedKinds.Contains(request.Kind))
            return false;

        return request.ProjectId.HasValue;
    }
}
