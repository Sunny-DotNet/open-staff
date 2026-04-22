using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.AgentSouls.Services;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Application.AgentRoles.Services;

/// <summary>
/// Parses role-template documents and resolves declared MCP/Skill dependencies against the local capability inventory.
/// </summary>
public sealed class AgentRoleTemplateImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex BarePropertyRegex = new(@"(?<prefix>[\{,]\s*)(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:", RegexOptions.Compiled);
    private static readonly Regex TrailingCommaRegex = new(@",(?=\s*[\}\]])", RegexOptions.Compiled);

    private readonly IAgentRoleRepository _agentRoles;
    private readonly IMcpServerRepository _mcpServers;
    private readonly IAgentRoleMcpBindingRepository _agentRoleMcpBindings;
    private readonly IAgentRoleSkillBindingRepository _agentRoleSkillBindings;
    private readonly IManagedSkillStore _managedSkillStore;
    private readonly IRepositoryContext _repositoryContext;
    private readonly IAgentSoulService? _agentSoulService;

    public AgentRoleTemplateImportService(
        IAgentRoleRepository agentRoles,
        IMcpServerRepository mcpServers,
        IAgentRoleMcpBindingRepository agentRoleMcpBindings,
        IAgentRoleSkillBindingRepository agentRoleSkillBindings,
        IManagedSkillStore managedSkillStore,
        IRepositoryContext repositoryContext,
        IAgentSoulService? agentSoulService = null)
    {
        _agentRoles = agentRoles;
        _mcpServers = mcpServers;
        _agentRoleMcpBindings = agentRoleMcpBindings;
        _agentRoleSkillBindings = agentRoleSkillBindings;
        _managedSkillStore = managedSkillStore;
        _repositoryContext = repositoryContext;
        _agentSoulService = agentSoulService;
    }

    public async Task<PreviewAgentRoleTemplateImportResultDto> PreviewAsync(string content, CancellationToken ct = default)
    {
        var document = ParseDocument(content);

        var servers = await _mcpServers.AsNoTracking()
            .Where(server => server.IsEnabled)
            .Select(server => new McpServerCandidate(
                server.Id,
                server.Name,
                server.Source,
                server.NpmPackage,
                server.PypiPackage,
                server.DefaultConfig,
                1))
            .ToListAsync(ct);

        var installedSkills = (await _managedSkillStore.GetInstalledAsync(ct))
            .Where(skill => string.Equals(skill.Status, SkillInstallStatuses.Installed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new PreviewAgentRoleTemplateImportResultDto
        {
            Role = new AgentRoleTemplatePreviewDto
            {
                ExternalId = document.Id,
                Name = document.Name?.Trim() ?? string.Empty,
                JobTitle = AgentJobTitleCatalog.NormalizeKey(FirstNonEmpty(document.Job, document.JobTitle)),
                Description = NormalizeOptional(document.Description),
                Avatar = NormalizeOptional(document.Avatar),
                ModelName = NormalizeOptional(document.Model),
                ModelConfig = NormalizeOptional(document.ModelConfig),
                Soul = document.Soul,
            },
            Mcps = (document.Mcps ?? [])
                .Select(requirement => ResolveMcpRequirement(requirement, servers))
                .ToList(),
            Skills = (document.Skills ?? [])
                .Select(requirement => ResolveSkillRequirement(requirement, installedSkills))
                .ToList(),
        };
    }

    public async Task<AgentRoleTemplateImportExecutionResult> ImportAsync(string content, bool overwriteExisting, CancellationToken ct = default)
    {
        var preview = await PreviewAsync(content, ct);
        if (string.IsNullOrWhiteSpace(preview.Role.Name))
            throw new InvalidOperationException("Role template must declare a name.");

        var role = await ResolveTargetRoleAsync(preview.Role, overwriteExisting, ct);
        await ApplyPreviewToRoleAsync(preview.Role, role);

        if (role.Id == Guid.Empty)
        {
            role.Id = ResolveImportedRoleId(preview.Role.ExternalId);
            await _agentRoles.AddAsync(role, ct);
        }

        role.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);

        var addedMcpBindings = await SyncMcpBindingsAsync(role, preview.Mcps, ct);
        var addedSkillBindings = await SyncSkillBindingsAsync(role, preview.Skills, ct);

        role.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);

        return new AgentRoleTemplateImportExecutionResult(role, preview, addedMcpBindings, addedSkillBindings);
    }

    private async Task<AgentRole> ResolveTargetRoleAsync(
        AgentRoleTemplatePreviewDto preview,
        bool overwriteExisting,
        CancellationToken ct)
    {
        var externalId = ParseGuid(preview.ExternalId);
        AgentRole? role = null;

        if (externalId.HasValue)
            role = await _agentRoles.FirstOrDefaultAsync(item => item.Id == externalId.Value && item.IsActive, ct);

        if (role is null)
        {
            role = (await _agentRoles
                    .Where(item => item.IsActive)
                    .ToListAsync(ct))
                .FirstOrDefault(item =>
                    string.Equals(item.Name, preview.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (role is null)
        {
            return new AgentRole
            {
                Source = AgentSource.Custom,
                IsBuiltin = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
        }

        if (!overwriteExisting)
            throw new InvalidOperationException($"Agent role '{role.Name}' already exists.");

        if (role.IsBuiltin || role.Source == AgentSource.Vendor)
            throw new InvalidOperationException($"Agent role '{role.Name}' cannot be overwritten from a template import.");

        return role;
    }

    private async Task ApplyPreviewToRoleAsync(AgentRoleTemplatePreviewDto preview, AgentRole role)
    {
        role.Name = preview.Name.Trim();
        role.JobTitle = preview.JobTitle;
        role.Description = preview.Description;
        role.Avatar = preview.Avatar;
        role.ModelName = preview.ModelName;
        role.Config = preview.ModelConfig;
        role.Soul = preview.Soul is null
            ? null
            : AgentRoleExecutionProfileFactory.MapSoulFromDto(await NormalizeSoulInputAsync(preview.Soul));
        role.Source = AgentSource.Custom;
        role.IsBuiltin = false;
        role.IsActive = true;
    }

    private async Task<AgentSoulDto> NormalizeSoulInputAsync(AgentSoulDto soul)
    {
        if (_agentSoulService is null)
        {
            return new AgentSoulDto
            {
                Traits = NormalizeSoulValues(soul.Traits),
                Style = NormalizeSoulValue(soul.Style),
                Attitudes = NormalizeSoulValues(soul.Attitudes),
                Custom = NormalizeSoulValue(soul.Custom)
            };
        }

        return new AgentSoulDto
        {
            Traits = await NormalizeSoulValuesAsync(soul.Traits, _agentSoulService.PersonalityTraits),
            Style = await NormalizeSoulKeyAsync(soul.Style, _agentSoulService.CommunicationStyles),
            Attitudes = await NormalizeSoulValuesAsync(soul.Attitudes, _agentSoulService.WorkAttitudes),
            Custom = NormalizeSoulValue(soul.Custom)
        };
    }

    private static List<string> NormalizeSoulValues(IEnumerable<string>? values)
        => values?
            .Select(NormalizeSoulValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
           ?? [];

    private static async Task<List<string>> NormalizeSoulValuesAsync(
        IEnumerable<string>? values,
        IAgentSoulHttpService service)
    {
        var results = new List<string>();
        if (values is null)
            return results;

        foreach (var value in values)
        {
            var normalized = await NormalizeSoulKeyAsync(value, service);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                results.Add(normalized);
        }

        return results;
    }

    private static async Task<string?> NormalizeSoulKeyAsync(string? value, IAgentSoulHttpService service)
    {
        var normalized = NormalizeSoulValue(value);
        if (normalized is null)
            return null;

        return await service.FindKeyAsync(normalized) ?? normalized;
    }

    private static string? NormalizeSoulValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<int> SyncMcpBindingsAsync(
        AgentRole role,
        IReadOnlyList<AgentRoleTemplateMcpRequirementDto> requirements,
        CancellationToken ct)
    {
        var desiredServerIds = requirements
            .Where(requirement =>
                string.Equals(requirement.Status, AgentRoleTemplateResolutionStatuses.Resolved, StringComparison.OrdinalIgnoreCase)
                && requirement.MatchedServerId.HasValue)
            .Select(requirement => requirement.MatchedServerId!.Value)
            .Distinct()
            .ToHashSet();

        var existingBindings = await _agentRoleMcpBindings
            .Where(binding => binding.AgentRoleId == role.Id)
            .ToListAsync(ct);

        var existingServerIds = existingBindings.Select(binding => binding.McpServerId).ToHashSet();

        var toRemove = existingBindings
            .Where(binding => !desiredServerIds.Contains(binding.McpServerId))
            .ToList();
        if (toRemove.Count > 0)
            _agentRoleMcpBindings.RemoveRange(toRemove);

        var missingServerIds = desiredServerIds
            .Where(serverId => !existingServerIds.Contains(serverId))
            .ToList();
        if (missingServerIds.Count == 0)
            return 0;

        var serverMap = await _mcpServers.AsNoTracking()
            .Where(server => missingServerIds.Contains(server.Id))
            .ToDictionaryAsync(server => server.Id, ct);

        var added = 0;
        foreach (var serverId in missingServerIds)
        {
            if (!serverMap.TryGetValue(serverId, out var server))
                continue;

            _agentRoleMcpBindings.Add(new AgentRoleMcpBinding
            {
                AgentRoleId = role.Id,
                McpServerId = server.Id,
                ToolFilter = null,
                IsEnabled = true,
            });
            added++;
        }

        return added;
    }

    private async Task<int> SyncSkillBindingsAsync(
        AgentRole role,
        IReadOnlyList<AgentRoleTemplateSkillRequirementDto> requirements,
        CancellationToken ct)
    {
        var desiredInstallKeys = requirements
            .Where(requirement =>
                string.Equals(requirement.Status, AgentRoleTemplateResolutionStatuses.Resolved, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(requirement.InstallKey))
            .Select(requirement => requirement.InstallKey!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingBindings = await _agentRoleSkillBindings
            .Where(binding => binding.AgentRoleId == role.Id)
            .ToListAsync(ct);

        var existingKeys = existingBindings
            .Select(binding => binding.SkillInstallKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRemove = existingBindings
            .Where(binding => !desiredInstallKeys.Contains(binding.SkillInstallKey))
            .ToList();
        if (toRemove.Count > 0)
            _agentRoleSkillBindings.RemoveRange(toRemove);

        var installedSkillsByKey = (await _managedSkillStore.GetInstalledAsync(ct))
            .Where(skill => string.Equals(skill.Status, SkillInstallStatuses.Installed, StringComparison.OrdinalIgnoreCase))
            .GroupBy(skill => skill.InstallKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var installKey in desiredInstallKeys)
        {
            if (existingKeys.Contains(installKey)
                || !installedSkillsByKey.TryGetValue(installKey, out var installedSkill))
            {
                continue;
            }

            _agentRoleSkillBindings.Add(new AgentRoleSkillBinding
            {
                AgentRoleId = role.Id,
                SkillInstallKey = installedSkill.InstallKey,
                SkillId = installedSkill.SkillId,
                Name = installedSkill.Name,
                DisplayName = installedSkill.DisplayName,
                Source = string.IsNullOrWhiteSpace(installedSkill.Source)
                    ? $"{installedSkill.Owner}/{installedSkill.Repo}"
                    : installedSkill.Source,
                Owner = installedSkill.Owner,
                Repo = installedSkill.Repo,
                GithubUrl = installedSkill.GithubUrl,
                IsEnabled = true,
            });
            added++;
        }

        return added;
    }

    private static PreviewRoleTemplateDocument ParseDocument(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Role template content is required.", nameof(content));

        var normalizedContent = NormalizeJsonishContent(content);

        try
        {
            return JsonSerializer.Deserialize<PreviewRoleTemplateDocument>(normalizedContent, JsonOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize role template.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Role template content is not valid JSON.", ex);
        }
    }

    private static string NormalizeJsonishContent(string content)
    {
        var normalized = content.Trim().Replace("\uFEFF", string.Empty, StringComparison.Ordinal);
        normalized = BarePropertyRegex.Replace(normalized, "${prefix}\"${key}\":");
        normalized = TrailingCommaRegex.Replace(normalized, string.Empty);
        return normalized;
    }

    private static AgentRoleTemplateMcpRequirementDto ResolveMcpRequirement(
        PreviewRoleTemplateMcpDocument requirement,
        IReadOnlyList<McpServerCandidate> servers)
    {
        var key = NormalizeOptional(requirement.Key);
        var name = FirstNonEmpty(requirement.McpServerName, requirement.Name);
        var npmPackage = NormalizeOptional(requirement.NpmPackage);
        var pypiPackage = NormalizeOptional(requirement.PypiPackage);

        var byKey = !string.IsNullOrWhiteSpace(key)
            ? servers.FirstOrDefault(server => McpServerMatchesKey(server, key))
            : null;
        if (byKey is not null)
        {
            return CreateResolvedMcpRequirement(key, name, npmPackage, pypiPackage, byKey, "key");
        }

        var byNpmPackage = !string.IsNullOrWhiteSpace(npmPackage)
            ? servers.FirstOrDefault(server => string.Equals(server.NpmPackage, npmPackage, StringComparison.OrdinalIgnoreCase))
            : null;
        if (byNpmPackage is not null)
        {
            return CreateResolvedMcpRequirement(key, name, npmPackage, pypiPackage, byNpmPackage, "npmPackage");
        }

        var byPypiPackage = !string.IsNullOrWhiteSpace(pypiPackage)
            ? servers.FirstOrDefault(server => string.Equals(server.PypiPackage, pypiPackage, StringComparison.OrdinalIgnoreCase))
            : null;
        if (byPypiPackage is not null)
        {
            return CreateResolvedMcpRequirement(key, name, npmPackage, pypiPackage, byPypiPackage, "pypiPackage");
        }

        var byName = !string.IsNullOrWhiteSpace(name)
            ? servers.FirstOrDefault(server => string.Equals(server.Name, name, StringComparison.OrdinalIgnoreCase))
            : null;
        if (byName is not null)
        {
            return CreateResolvedMcpRequirement(key, name, npmPackage, pypiPackage, byName, "name");
        }

        return new AgentRoleTemplateMcpRequirementDto
        {
            Key = key,
            Name = name,
            NpmPackage = npmPackage,
            PypiPackage = pypiPackage,
            Status = AgentRoleTemplateResolutionStatuses.Missing,
            Message = "No local MCP server matched this requirement.",
        };
    }

    private static AgentRoleTemplateMcpRequirementDto CreateResolvedMcpRequirement(
        string? key,
        string? name,
        string? npmPackage,
        string? pypiPackage,
        McpServerCandidate server,
        string matchStrategy)
        => new()
        {
            Key = key,
            Name = name,
            NpmPackage = npmPackage,
            PypiPackage = pypiPackage,
            Status = AgentRoleTemplateResolutionStatuses.Resolved,
            MatchStrategy = matchStrategy,
            Message = $"Resolved to local MCP server '{server.Name}'.",
            MatchedServerId = server.Id,
            MatchedServerName = server.Name,
            MatchedServerSource = server.Source,
            ConfigCount = server.ConfigCount,
        };

    private static AgentRoleTemplateSkillRequirementDto ResolveSkillRequirement(
        PreviewRoleTemplateSkillDocument requirement,
        IReadOnlyList<ManagedInstalledSkill> installedSkills)
    {
        var key = NormalizeOptional(requirement.Key);
        var source = NormalizeOptional(requirement.Source);
        var sourceKey = NormalizeOptional(requirement.SourceKey);
        var owner = NormalizeOptional(requirement.Owner);
        var repo = NormalizeOptional(requirement.Repo);
        var skillId = requirement.SkillId?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(key))
        {
            var byInstallKey = installedSkills.FirstOrDefault(skill =>
                string.Equals(skill.InstallKey, key, StringComparison.OrdinalIgnoreCase));
            if (byInstallKey is not null)
            {
                return new AgentRoleTemplateSkillRequirementDto
                {
                    Key = key,
                    Source = string.IsNullOrWhiteSpace(byInstallKey.Source)
                        ? $"{byInstallKey.Owner}/{byInstallKey.Repo}"
                        : byInstallKey.Source,
                    SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? byInstallKey.SourceKey : sourceKey,
                    Owner = byInstallKey.Owner,
                    Repo = byInstallKey.Repo,
                    SkillId = byInstallKey.SkillId,
                    Status = AgentRoleTemplateResolutionStatuses.Resolved,
                    MatchStrategy = "key",
                    Message = $"Resolved to installed skill '{byInstallKey.DisplayName}'.",
                    InstallKey = byInstallKey.InstallKey,
                    DisplayName = byInstallKey.DisplayName,
                };
            }

            var parsedKey = ParseSkillKey(key);
            sourceKey ??= parsedKey.SourceKey;
            owner ??= parsedKey.Owner;
            repo ??= parsedKey.Repo;
            if (string.IsNullOrWhiteSpace(skillId))
                skillId = parsedKey.SkillId ?? string.Empty;
        }

        if ((string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            && !string.IsNullOrWhiteSpace(source))
        {
            (owner, repo) = ParseRepositoryFromSource(source, owner, repo);
        }

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(skillId))
        {
            return new AgentRoleTemplateSkillRequirementDto
            {
                Key = key,
                Source = source,
                SourceKey = sourceKey,
                Owner = owner,
                Repo = repo,
                SkillId = skillId,
                Status = AgentRoleTemplateResolutionStatuses.Missing,
                Message = "The template does not declare a complete skill identity.",
            };
        }

        var installed = installedSkills.FirstOrDefault(skill =>
            string.Equals(skill.Owner, owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(skill.Repo, repo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(skill.SkillId, skillId, StringComparison.OrdinalIgnoreCase));

        if (installed is null)
        {
            return new AgentRoleTemplateSkillRequirementDto
            {
                Key = key,
                Source = source,
                SourceKey = sourceKey,
                Owner = owner,
                Repo = repo,
                SkillId = skillId,
                Status = AgentRoleTemplateResolutionStatuses.Missing,
                Message = "No installed skill matched this requirement.",
            };
        }

        return new AgentRoleTemplateSkillRequirementDto
        {
            Key = key,
            Source = source,
            SourceKey = sourceKey ?? installed.SourceKey,
            Owner = owner,
            Repo = repo,
            SkillId = skillId,
            Status = AgentRoleTemplateResolutionStatuses.Resolved,
            MatchStrategy = "owner/repo/skillId",
            Message = $"Resolved to installed skill '{installed.DisplayName}'.",
            InstallKey = installed.InstallKey,
            DisplayName = installed.DisplayName,
        };
    }

    private static (string? Owner, string? Repo) ParseRepositoryFromSource(string source, string? owner, string? repo)
    {
        if (!string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo))
            return (owner, repo);

        var segments = source.Trim()
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2)
            return (segments[0], segments[1]);

        if (segments.Length >= 3)
            return (segments[1], segments[2]);

        return (owner, repo);
    }

    private static SkillKeyIdentity ParseSkillKey(string key)
    {
        var parts = key.Trim()
            .Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return new SkillKeyIdentity(null, null, null, null);

        var repositorySegments = parts[0]
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (repositorySegments.Length == 2)
            return new SkillKeyIdentity(null, repositorySegments[0], repositorySegments[1], parts[1]);

        if (repositorySegments.Length >= 3)
            return new SkillKeyIdentity(repositorySegments[0], repositorySegments[^2], repositorySegments[^1], parts[1]);

        return new SkillKeyIdentity(null, null, null, parts[1]);
    }

    private static bool McpServerMatchesKey(McpServerCandidate server, string key)
    {
        var normalizedKey = NormalizeCapabilityKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        if (string.Equals(NormalizeCapabilityKey(server.Name), normalizedKey, StringComparison.Ordinal))
            return true;

        var templateKey = ExtractTemplateKey(server.TemplateJson);
        return !string.IsNullOrWhiteSpace(templateKey)
               && string.Equals(NormalizeCapabilityKey(templateKey), normalizedKey, StringComparison.Ordinal);
    }

    private static string? ExtractTemplateKey(string? templateJson)
    {
        if (string.IsNullOrWhiteSpace(templateJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(templateJson);
            if (document.RootElement.TryGetProperty("key", out var keyProperty)
                && keyProperty.ValueKind == JsonValueKind.String)
            {
                return NormalizeOptional(keyProperty.GetString());
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string NormalizeCapabilityKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static Guid ResolveImportedRoleId(string? externalId)
        => ParseGuid(externalId) ?? Guid.NewGuid();

    private static Guid? ParseGuid(string? externalId)
        => Guid.TryParse(externalId, out var parsed) ? parsed : null;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonEmpty(params string?[] values)
        => values.Select(NormalizeOptional).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record McpServerCandidate(
        Guid Id,
        string Name,
        string Source,
        string? NpmPackage,
        string? PypiPackage,
        string? TemplateJson,
        int ConfigCount);

    private sealed record SkillKeyIdentity(
        string? SourceKey,
        string? Owner,
        string? Repo,
        string? SkillId);

    private sealed class PreviewRoleTemplateDocument
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Job { get; set; }

        public string? JobTitle { get; set; }

        public string? Description { get; set; }

        public string? Avatar { get; set; }

        public string? Model { get; set; }

        public string? ModelConfig { get; set; }

        public AgentSoulDto? Soul { get; set; }

        public List<PreviewRoleTemplateMcpDocument>? Mcps { get; set; }

        public List<PreviewRoleTemplateSkillDocument>? Skills { get; set; }
    }

    private sealed class PreviewRoleTemplateMcpDocument
    {
        public string? Key { get; set; }

        public string? McpServerName { get; set; }

        public string? Name { get; set; }

        public string? NpmPackage { get; set; }

        public string? PypiPackage { get; set; }
    }

    private sealed class PreviewRoleTemplateSkillDocument
    {
        public string? Key { get; set; }

        public string? Source { get; set; }

        public string? SourceKey { get; set; }

        public string? Owner { get; set; }

        public string? Repo { get; set; }

        public string? SkillId { get; set; }
    }
}

public sealed record AgentRoleTemplateImportExecutionResult(
    AgentRole Role,
    PreviewAgentRoleTemplateImportResultDto Preview,
    int AddedMcpBindings,
    int AddedSkillBindings);
