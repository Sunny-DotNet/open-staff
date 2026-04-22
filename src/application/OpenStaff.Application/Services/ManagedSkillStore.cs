using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenStaff.Entities;
using OpenStaff.Options;

namespace OpenStaff.Application.Skills.Services;
/// <summary>
/// zh-CN: 扫描与维护 OpenStaff 自管的 skill 规范仓库。
/// en: Scans and maintains the canonical managed-skill store owned by OpenStaff.
/// </summary>
public interface IManagedSkillStore
{
    Task<IReadOnlyList<ManagedInstalledSkill>> GetInstalledAsync(CancellationToken ct = default);
    Task<ManagedInstalledSkill?> GetByInstallKeyAsync(string installKey, CancellationToken ct = default);
    Task<ManagedInstalledSkill?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ManagedInstalledSkill> InstallAsync(SkillCatalogEntry catalogItem, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<SkillStoreMaintenanceResult> CheckForUpdatesAsync(CancellationToken ct = default);
    Task<SkillStoreMaintenanceResult> UpdateAsync(CancellationToken ct = default);
}

/// <summary>
/// zh-CN: 自管 skill 安装项。
/// en: Managed skill installation item.
/// </summary>
public sealed record ManagedInstalledSkill(
    Guid Id,
    string InstallKey,
    string SourceKey,
    string Source,
    string Owner,
    string Repo,
    string SkillId,
    string Name,
    string DisplayName,
    string? GithubUrl,
    int Installs,
    string InstallRootPath,
    string Status,
    string? StatusMessage,
    string? SourceRevision,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// zh-CN: Skill 仓库维护结果。
/// en: Maintenance result for the managed skill store.
/// </summary>
public sealed record SkillStoreMaintenanceResult(
    bool Success,
    int CheckedCount,
    int OutdatedCount,
    int UpdatedCount,
    string Summary);

internal sealed class ManagedSkillStore : IManagedSkillStore
{
    private const string GitHubClientName = "skills-github";
    private const string ManifestFileName = ".openstaff-install.json";
    private static readonly Regex InstallKeySanitizer = new("[^A-Za-z0-9._-]+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenStaffOptions _openStaffOptions;
    private readonly ILogger<ManagedSkillStore> _logger;
    private readonly Func<string, string, CancellationToken, Task<string>>? _gitHeadResolver;

    public ManagedSkillStore(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenStaffOptions> openStaffOptions,
        ILogger<ManagedSkillStore> logger,
        Func<string, string, CancellationToken, Task<string>>? gitHeadResolver = null)
    {
        _httpClientFactory = httpClientFactory;
        _openStaffOptions = openStaffOptions.Value;
        _logger = logger;
        _gitHeadResolver = gitHeadResolver;
    }

    public async Task<IReadOnlyList<ManagedInstalledSkill>> GetInstalledAsync(CancellationToken ct = default)
    {
        var root = EnsureStoreRoot();
        var items = new List<ManagedInstalledSkill>();

        foreach (var directory in Directory.GetDirectories(root))
        {
            var directoryName = Path.GetFileName(directory);
            if (directoryName.StartsWith(".tmp-", StringComparison.OrdinalIgnoreCase)
                || directoryName.StartsWith(".backup-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath))
                continue;

            ManagedSkillManifest? manifest;
            try
            {
                manifest = await ReadManifestAsync(manifestPath, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read managed skill manifest from {ManifestPath}", manifestPath);
                continue;
            }

            if (manifest is null)
                continue;

            var status = File.Exists(Path.Combine(directory, "SKILL.md"))
                ? SkillInstallStatuses.Installed
                : SkillInstallStatuses.Missing;
            var statusMessage = status == SkillInstallStatuses.Installed
                ? null
                : "Skill 目录缺少 SKILL.md，运行时将跳过该安装项。";

            items.Add(ToManagedInstalledSkill(manifest, directory, status, statusMessage));
        }

        return items
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ManagedInstalledSkill?> GetByInstallKeyAsync(string installKey, CancellationToken ct = default)
        => (await GetInstalledAsync(ct)).FirstOrDefault(item =>
            string.Equals(item.InstallKey, installKey, StringComparison.OrdinalIgnoreCase));

    public async Task<ManagedInstalledSkill?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => (await GetInstalledAsync(ct)).FirstOrDefault(item => item.Id == id);

    public async Task<ManagedInstalledSkill> InstallAsync(SkillCatalogEntry catalogItem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalogItem);

        var installKey = BuildInstallKey(catalogItem.Owner, catalogItem.Repo, catalogItem.SkillId);
        var root = EnsureStoreRoot();
        var targetDirectory = Path.Combine(root, installKey);
        var existing = await GetByInstallKeyAsync(installKey, ct);
        var now = DateTime.UtcNow;
        var headSha = await GetLatestCommitShaAsync(catalogItem.Owner, catalogItem.Repo, ct);

        var tempRoot = Path.Combine(Path.GetTempPath(), "openstaff-skills", Guid.NewGuid().ToString("N"));
        var tempZipFile = Path.Combine(tempRoot, "repo.zip");
        var tempExtractDirectory = Path.Combine(tempRoot, "extract");
        var stageDirectory = Path.Combine(root, $".tmp-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(root, $".backup-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(tempExtractDirectory);

            await DownloadRepositoryArchiveAsync(catalogItem.Owner, catalogItem.Repo, headSha, tempZipFile, ct);
            ZipFile.ExtractToDirectory(tempZipFile, tempExtractDirectory);

            var repoRoot = Directory.GetDirectories(tempExtractDirectory).FirstOrDefault()
                ?? throw new InvalidOperationException($"Repository archive '{catalogItem.Owner}/{catalogItem.Repo}' is empty.");
            var skillDirectory = ResolveSkillDirectory(repoRoot, catalogItem.SkillId);

            CopyDirectory(skillDirectory, stageDirectory);

            var manifest = new ManagedSkillManifest
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                InstallKey = installKey,
                SourceKey = string.IsNullOrWhiteSpace(catalogItem.SourceKey) ? SkillSourceKeys.SkillsSh : catalogItem.SourceKey,
                Source = $"{catalogItem.Owner}/{catalogItem.Repo}",
                Owner = catalogItem.Owner,
                Repo = catalogItem.Repo,
                SkillId = catalogItem.SkillId,
                Name = catalogItem.Name,
                DisplayName = catalogItem.DisplayName,
                GithubUrl = catalogItem.RepositoryUrl,
                Installs = catalogItem.Installs,
                SourceRevision = headSha,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = now
            };

            await WriteManifestAsync(Path.Combine(stageDirectory, ManifestFileName), manifest, ct);

            if (Directory.Exists(targetDirectory))
                Directory.Move(targetDirectory, backupDirectory);

            Directory.Move(stageDirectory, targetDirectory);

            if (Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, recursive: true);

            return ToManagedInstalledSkill(manifest, targetDirectory, SkillInstallStatuses.Installed, null);
        }
        catch
        {
            if (Directory.Exists(stageDirectory))
                Directory.Delete(stageDirectory, recursive: true);

            if (Directory.Exists(backupDirectory) && !Directory.Exists(targetDirectory))
                Directory.Move(backupDirectory, targetDirectory);

            throw;
        }
        finally
        {
            CleanupDirectory(tempRoot);
        }
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await GetByIdAsync(id, ct);
        if (existing is null)
            return false;

        if (Directory.Exists(existing.InstallRootPath))
            Directory.Delete(existing.InstallRootPath, recursive: true);

        return true;
    }

    public async Task<SkillStoreMaintenanceResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var installed = await GetInstalledAsync(ct);
        var managedInstalled = installed
            .Where(item => string.Equals(item.Status, SkillInstallStatuses.Installed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var outdatedCount = 0;
        foreach (var item in managedInstalled)
        {
            var latestSha = await GetLatestCommitShaAsync(item.Owner, item.Repo, ct);
            if (!string.Equals(latestSha, item.SourceRevision, StringComparison.OrdinalIgnoreCase))
                outdatedCount++;
        }

        return new SkillStoreMaintenanceResult(
            Success: true,
            CheckedCount: managedInstalled.Count,
            OutdatedCount: outdatedCount,
            UpdatedCount: 0,
            Summary: outdatedCount == 0
                ? $"已检查 {managedInstalled.Count} 个技能，全部为最新版本。"
                : $"已检查 {managedInstalled.Count} 个技能，发现 {outdatedCount} 个可更新。");
    }

    public async Task<SkillStoreMaintenanceResult> UpdateAsync(CancellationToken ct = default)
    {
        var installed = await GetInstalledAsync(ct);
        var managedInstalled = installed
            .Where(item => string.Equals(item.Status, SkillInstallStatuses.Installed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var updatedCount = 0;
        foreach (var item in managedInstalled)
        {
            var latestSha = await GetLatestCommitShaAsync(item.Owner, item.Repo, ct);
            if (string.Equals(latestSha, item.SourceRevision, StringComparison.OrdinalIgnoreCase))
                continue;

            await InstallAsync(
                new SkillCatalogEntry
                {
                    SourceKey = item.SourceKey,
                    Owner = item.Owner,
                    Repo = item.Repo,
                    SkillId = item.SkillId,
                    Name = item.Name,
                    DisplayName = item.DisplayName,
                    Description = null,
                    RepositoryUrl = item.GithubUrl,
                    Installs = item.Installs
                },
                ct);
            updatedCount++;
        }

        return new SkillStoreMaintenanceResult(
            Success: true,
            CheckedCount: managedInstalled.Count,
            OutdatedCount: updatedCount,
            UpdatedCount: updatedCount,
            Summary: updatedCount == 0
                ? $"已检查 {managedInstalled.Count} 个技能，无需更新。"
                : $"已更新 {updatedCount} 个技能。");
    }

    private string EnsureStoreRoot()
    {
        var root = Path.Combine(_openStaffOptions.WorkingDirectory, "skills");
        Directory.CreateDirectory(root);
        return root;
    }

    private async Task DownloadRepositoryArchiveAsync(
        string owner,
        string repo,
        string sha,
        string targetZipFile,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(GitHubClientName);
        using var response = await client.GetAsync(
            new Uri($"https://codeload.github.com/{owner}/{repo}/zip/{sha}"),
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
        await using var targetStream = File.Create(targetZipFile);
        await sourceStream.CopyToAsync(targetStream, ct);
    }

    private async Task<string> GetLatestCommitShaAsync(string owner, string repo, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(GitHubClientName);
            var commits = await client.GetFromJsonAsync<List<GitHubCommitSummary>>(
                $"repos/{owner}/{repo}/commits?per_page=1",
                cancellationToken: ct)
                ?? [];

            return commits.FirstOrDefault()?.Sha
                ?? throw new InvalidOperationException($"Unable to determine the latest commit for '{owner}/{repo}'.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve latest commit via GitHub API for {Owner}/{Repo}, falling back to git ls-remote", owner, repo);
            return await ResolveLatestCommitShaWithGitAsync(owner, repo, ct);
        }
    }

    private Task<string> ResolveLatestCommitShaWithGitAsync(string owner, string repo, CancellationToken ct)
        => _gitHeadResolver is not null
            ? _gitHeadResolver(owner, repo, ct)
            : ResolveLatestCommitShaWithGitProcessAsync(owner, repo, ct);

    private static async Task<string> ResolveLatestCommitShaWithGitProcessAsync(string owner, string repo, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git", $"ls-remote https://github.com/{owner}/{repo} HEAD")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start git ls-remote for '{owner}/{repo}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git ls-remote failed for '{owner}/{repo}': {stderr}".Trim());

        var firstToken = stdout
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstToken))
            throw new InvalidOperationException($"git ls-remote did not return a commit SHA for '{owner}/{repo}'.");

        return firstToken.Trim();
    }

    private static string ResolveSkillDirectory(string repoRoot, string skillId)
    {
        var candidates = Directory.GetFiles(repoRoot, "SKILL.md", SearchOption.AllDirectories)
            .Select(skillFile => new SkillCandidate(
                DirectoryPath: Path.GetDirectoryName(skillFile)!,
                SkillName: ReadSkillName(skillFile)))
            .Where(candidate =>
                string.Equals(Path.GetFileName(candidate.DirectoryPath), skillId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.SkillName, skillId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => string.Equals(candidate.SkillName, skillId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(candidate => candidate.DirectoryPath.Count(ch => ch == Path.DirectorySeparatorChar))
            .ToList();

        if (candidates.Count == 1)
            return candidates[0].DirectoryPath;

        if (candidates.Count > 1)
            return candidates[0].DirectoryPath;

        var fallback = Directory.GetFiles(repoRoot, "SKILL.md", SearchOption.AllDirectories)
            .Select(skillFile => Path.GetDirectoryName(skillFile)!)
            .ToList();
        if (fallback.Count == 1)
            return fallback[0];

        throw new InvalidOperationException($"Skill '{skillId}' was not found in the downloaded repository archive.");
    }

    private static string? ReadSkillName(string skillFile)
    {
        using var reader = new StreamReader(skillFile);
        if (!string.Equals(reader.ReadLine()?.Trim(), "---", StringComparison.Ordinal))
            return null;

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is null)
                break;

            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
                break;

            const string prefix = "name:";
            if (!line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            return line[(line.IndexOf(':') + 1)..].Trim().Trim('\'', '"');
        }

        return null;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: true);
        }
    }

    private static void CleanupDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }

    private static string BuildInstallKey(string owner, string repo, string skillId)
        => string.Join(
            "--",
            SanitizeInstallKeySegment(owner),
            SanitizeInstallKeySegment(repo),
            SanitizeInstallKeySegment(skillId));

    private static string SanitizeInstallKeySegment(string value)
        => InstallKeySanitizer.Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');

    private static async Task<ManagedSkillManifest?> ReadManifestAsync(string manifestPath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<ManagedSkillManifest>(stream, ManifestJsonOptions, ct);
    }

    private static async Task WriteManifestAsync(string manifestPath, ManagedSkillManifest manifest, CancellationToken ct)
    {
        await using var stream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, ManifestJsonOptions, ct);
    }

    private static ManagedInstalledSkill ToManagedInstalledSkill(
        ManagedSkillManifest manifest,
        string installRootPath,
        string status,
        string? statusMessage)
        => new(
            Id: manifest.Id,
            InstallKey: manifest.InstallKey,
            SourceKey: manifest.SourceKey,
            Source: manifest.Source,
            Owner: manifest.Owner,
            Repo: manifest.Repo,
            SkillId: manifest.SkillId,
            Name: manifest.Name,
            DisplayName: manifest.DisplayName,
            GithubUrl: manifest.GithubUrl,
            Installs: manifest.Installs,
            InstallRootPath: installRootPath,
            Status: status,
            StatusMessage: statusMessage,
            SourceRevision: manifest.SourceRevision,
            CreatedAt: manifest.CreatedAt,
            UpdatedAt: manifest.UpdatedAt);

    private sealed record SkillCandidate(string DirectoryPath, string? SkillName);

    private sealed class ManagedSkillManifest
    {
        public Guid Id { get; set; }
        public string InstallKey { get; set; } = string.Empty;
        public string SourceKey { get; set; } = SkillSourceKeys.SkillsSh;
        public string Source { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string SkillId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? GithubUrl { get; set; }
        public int Installs { get; set; }
        public string? SourceRevision { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class GitHubCommitSummary
    {
        public string Sha { get; set; } = string.Empty;
    }
}

