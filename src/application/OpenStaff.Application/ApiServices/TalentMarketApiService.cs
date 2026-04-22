using Microsoft.EntityFrameworkCore;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.Repositories;
using OpenStaff.TalentMarket.Services;

namespace OpenStaff.ApiServices;

public sealed class TalentMarketApiService : ApiServiceBase, ITalentMarketApiService
{
    private readonly ITalentMarketRemoteCatalogService _remoteCatalogService;
    private readonly IAgentRoleRepository _agentRoles;
    private readonly IAgentRoleApiService _agentRoleApiService;

    public TalentMarketApiService(
        ITalentMarketRemoteCatalogService remoteCatalogService,
        IAgentRoleRepository agentRoles,
        IAgentRoleApiService agentRoleApiService,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _remoteCatalogService = remoteCatalogService;
        _agentRoles = agentRoles;
        _agentRoleApiService = agentRoleApiService;
    }

    public Task<List<TalentMarketSourceDto>> GetSourcesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new List<TalentMarketSourceDto>
        {
            new()
            {
                SourceKey = _remoteCatalogService.SourceKey,
                DisplayName = _remoteCatalogService.DisplayName
            }
        });
    }

    public async Task<TalentMarketSearchResultDto> SearchAsync(TalentMarketSearchQueryDto query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!string.IsNullOrWhiteSpace(query.SourceKey)
            && !string.Equals(query.SourceKey, _remoteCatalogService.SourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return new TalentMarketSearchResultDto();
        }

        var keyword = NormalizeOptional(query.Keyword);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 12 : query.PageSize, 1, 100);

        var templates = await _remoteCatalogService.GetTemplatesAsync(ct);
        var filtered = templates
            .Where(item => MatchesKeyword(item, keyword))
            .ToList();

        var pageItems = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var localRoles = await LoadLocalRolesAsync(ct);
        return new TalentMarketSearchResultDto
        {
            TotalCount = filtered.Count,
            Items = pageItems.Select(item => MapSummary(item, ResolveLocalMatch(item, localRoles))).ToList()
        };
    }

    public async Task<TalentMarketHirePreviewDto> PreviewHireAsync(PreviewTalentMarketHireInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureSource(input.SourceKey);

        var template = await GetRequiredTemplateAsync(input.TemplateId, ct);
        var preview = await _agentRoleApiService.PreviewTemplateImportAsync(
            new PreviewAgentRoleTemplateImportInput { Content = template.Content },
            ct);

        var match = await ResolveLocalMatchAsync(preview.Role.ExternalId, preview.Role.Name, ct);
        return new TalentMarketHirePreviewDto
        {
            SourceKey = _remoteCatalogService.SourceKey,
            Template = MapSummary(template.Summary, match),
            Preview = preview,
            MatchedRoleId = match?.RoleId,
            MatchedRoleName = match?.RoleName,
            CanOverwrite = match?.CanOverwrite ?? true,
            RequiresOverwriteConfirmation = match is not null && match.CanOverwrite,
            OverwriteBlockedReason = match is not null && !match.CanOverwrite
                ? $"Local role '{match.RoleName}' is builtin or vendor-backed and cannot be overwritten."
                : null
        };
    }

    public async Task<ImportAgentRoleTemplateResultDto> HireAsync(HireTalentMarketRoleInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureSource(input.SourceKey);

        var template = await GetRequiredTemplateAsync(input.TemplateId, ct);
        return await _agentRoleApiService.ImportTemplateAsync(
            new ImportAgentRoleTemplateInput
            {
                Content = template.Content,
                OverwriteExisting = input.OverwriteExisting
            },
            ct);
    }

    private void EnsureSource(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)
            || string.Equals(sourceKey.Trim(), _remoteCatalogService.SourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new KeyNotFoundException($"Talent-market source '{sourceKey}' was not found.");
    }

    private async Task<RemoteTalentMarketTemplate> GetRequiredTemplateAsync(string? templateId, CancellationToken ct)
        => await _remoteCatalogService.GetTemplateAsync(templateId ?? string.Empty, ct)
           ?? throw new KeyNotFoundException($"Talent-market template '{templateId}' was not found.");

    private async Task<List<LocalRoleRecord>> LoadLocalRolesAsync(CancellationToken ct)
        => await _agentRoles.AsNoTracking()
            .Where(role => role.IsActive)
            .Select(role => new LocalRoleRecord(
                role.Id,
                role.Name,
                role.IsBuiltin,
                role.Source))
            .ToListAsync(ct);

    private async Task<LocalRoleMatch?> ResolveLocalMatchAsync(string? externalId, string? roleName, CancellationToken ct)
        => ResolveLocalMatch(
            new RemoteTalentMarketTemplateSummary(
                externalId?.Trim() ?? string.Empty,
                string.Empty,
                roleName?.Trim() ?? string.Empty,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                true,
                0,
                0),
            await LoadLocalRolesAsync(ct));

    private static LocalRoleMatch? ResolveLocalMatch(
        RemoteTalentMarketTemplateSummary template,
        IReadOnlyList<LocalRoleRecord> roles)
    {
        LocalRoleRecord? matchedRole = null;
        if (Guid.TryParse(template.TemplateId, out var templateGuid))
            matchedRole = roles.FirstOrDefault(role => role.Id == templateGuid);

        matchedRole ??= roles.FirstOrDefault(role =>
            string.Equals(role.Name, template.Name, StringComparison.OrdinalIgnoreCase));

        if (matchedRole is null)
            return null;

        return new LocalRoleMatch(
            matchedRole.Id,
            matchedRole.Name,
            !matchedRole.IsBuiltin && matchedRole.Source != AgentSource.Vendor);
    }

    private TalentMarketRoleSummaryDto MapSummary(
        RemoteTalentMarketTemplateSummary template,
        LocalRoleMatch? match)
        => new()
        {
            TemplateId = template.TemplateId,
            SourceKey = _remoteCatalogService.SourceKey,
            File = template.File,
            Name = template.Name,
            Job = template.Job,
            JobTitle = FirstNonEmpty(template.JobTitle, template.Job),
            Description = template.Description,
            Avatar = template.Avatar,
            ModelName = template.ModelName,
            Source = template.Source,
            IsBuiltin = template.IsBuiltin,
            IsActive = template.IsActive,
            McpCount = template.McpCount,
            SkillCount = template.SkillCount,
            IsHired = match is not null,
            MatchedRoleId = match?.RoleId,
            MatchedRoleName = match?.RoleName,
            CanOverwrite = match?.CanOverwrite ?? true
        };

    private static bool MatchesKeyword(RemoteTalentMarketTemplateSummary template, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return true;

        return Contains(template.Name, keyword)
            || Contains(template.Job, keyword)
            || Contains(template.JobTitle, keyword)
            || Contains(template.Description, keyword)
            || Contains(template.ModelName, keyword)
            || Contains(template.Source, keyword);
    }

    private static bool Contains(string? value, string keyword)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonEmpty(params string?[] values)
        => values.Select(NormalizeOptional).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record LocalRoleRecord(
        Guid Id,
        string Name,
        bool IsBuiltin,
        AgentSource Source);

    private sealed record LocalRoleMatch(
        Guid RoleId,
        string RoleName,
        bool CanOverwrite);
}
