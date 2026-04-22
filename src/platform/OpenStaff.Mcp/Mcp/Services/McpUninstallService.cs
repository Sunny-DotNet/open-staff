using OpenStaff.Mcp.Exceptions;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 负责卸载前检查与安全清理。
/// en: Handles uninstall pre-checks and safe cleanup.
/// </summary>
public sealed class McpUninstallService : IMcpUninstallService
{
    private readonly IInstalledMcpMetadataStore _metadataStore;
    private readonly IMcpManifestStore _manifestStore;
    private readonly IMcpDataDirectoryLayout _layout;
    private readonly IReadOnlyList<IMcpReferenceInspector> _referenceInspectors;

    public McpUninstallService(
        IInstalledMcpMetadataStore metadataStore,
        IMcpManifestStore manifestStore,
        IMcpDataDirectoryLayout layout,
        IEnumerable<IMcpReferenceInspector> referenceInspectors)
    {
        _metadataStore = metadataStore;
        _manifestStore = manifestStore;
        _layout = layout;
        _referenceInspectors = referenceInspectors.ToList();
    }

    public async Task<UninstallCheckResult> CheckUninstallAsync(Guid installId, CancellationToken cancellationToken = default)
    {
        var installed = await _metadataStore.GetAsync(installId, cancellationToken)
            ?? throw new KeyNotFoundException($"Install '{installId}' does not exist.");

        var blockingReasons = new List<string>();
        var referencedByConfigs = new List<string>();
        var referencedByProjectBindings = new List<string>();
        var referencedByRoleBindings = new List<string>();

        foreach (var inspector in _referenceInspectors)
        {
            var inspection = await inspector.InspectAsync(installed, cancellationToken);
            blockingReasons.AddRange(inspection.BlockingReasons);
            referencedByConfigs.AddRange(inspection.ReferencedByConfigs);
            referencedByProjectBindings.AddRange(inspection.ReferencedByProjectBindings);
            referencedByRoleBindings.AddRange(inspection.ReferencedByRoleBindings);
        }

        return new UninstallCheckResult
        {
            CanUninstall = blockingReasons.Count == 0
                           && referencedByConfigs.Count == 0
                           && referencedByProjectBindings.Count == 0
                           && referencedByRoleBindings.Count == 0,
            BlockingReasons = blockingReasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReferencedByConfigs = referencedByConfigs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReferencedByProjectBindings = referencedByProjectBindings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReferencedByRoleBindings = referencedByRoleBindings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    public async Task UninstallAsync(Guid installId, CancellationToken cancellationToken = default)
    {
        var installed = await _metadataStore.GetAsync(installId, cancellationToken)
            ?? throw new KeyNotFoundException($"Install '{installId}' does not exist.");

        var check = await CheckUninstallAsync(installId, cancellationToken);
        if (!check.CanUninstall)
        {
            var allReasons = check.BlockingReasons
                .Concat(check.ReferencedByConfigs.Select(item => $"config:{item}"))
                .Concat(check.ReferencedByProjectBindings.Select(item => $"project-binding:{item}"))
                .Concat(check.ReferencedByRoleBindings.Select(item => $"role-binding:{item}"));
            throw new UninstallBlockedException(string.Join("; ", allReasons));
        }

        installed.InstallState = InstallState.Uninstalling;
        installed.UpdatedAt = DateTime.UtcNow;
        await _metadataStore.UpsertAsync(installed, cancellationToken);

        await _manifestStore.DeleteAsync(installed.ManifestPath, cancellationToken);

        if (Directory.Exists(installed.InstallDirectory))
            Directory.Delete(installed.InstallDirectory, recursive: true);

        var extractCachePath = _layout.GetExtractCachePath(installId);
        if (Directory.Exists(extractCachePath))
            Directory.Delete(extractCachePath, recursive: true);

        var downloadsDirectory = _layout.GetDownloadsCacheDirectory();
        foreach (var file in Directory.GetFiles(downloadsDirectory, $"{installId:N}.*", SearchOption.TopDirectoryOnly))
            File.Delete(file);

        await _metadataStore.DeleteAsync(installId, cancellationToken);
    }
}
