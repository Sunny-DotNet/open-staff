namespace OpenStaff.Dtos;

public class TalentMarketSourceDto
{
    public string SourceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public class TalentMarketSearchQueryDto
{
    public string? SourceKey { get; set; }

    public string? Keyword { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}

public class TalentMarketRoleSummaryDto
{
    public string TemplateId { get; set; } = string.Empty;

    public string SourceKey { get; set; } = string.Empty;

    public string? File { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Job { get; set; }

    public string? JobTitle { get; set; }

    public string? Description { get; set; }

    public string? Avatar { get; set; }

    public string? ModelName { get; set; }

    public string? Source { get; set; }

    public bool IsBuiltin { get; set; }

    public bool IsActive { get; set; }

    public int McpCount { get; set; }

    public int SkillCount { get; set; }

    public bool IsHired { get; set; }

    public Guid? MatchedRoleId { get; set; }

    public string? MatchedRoleName { get; set; }

    public bool CanOverwrite { get; set; }
}

public class TalentMarketSearchResultDto
{
    public List<TalentMarketRoleSummaryDto> Items { get; set; } = [];

    public int TotalCount { get; set; }
}

public class PreviewTalentMarketHireInput
{
    public string SourceKey { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;
}

public class HireTalentMarketRoleInput
{
    public string SourceKey { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public bool OverwriteExisting { get; set; }
}

public class TalentMarketHirePreviewDto
{
    public string SourceKey { get; set; } = string.Empty;

    public TalentMarketRoleSummaryDto Template { get; set; } = new();

    public PreviewAgentRoleTemplateImportResultDto Preview { get; set; } = new();

    public Guid? MatchedRoleId { get; set; }

    public string? MatchedRoleName { get; set; }

    public bool CanOverwrite { get; set; }

    public bool RequiresOverwriteConfirmation { get; set; }

    public string? OverwriteBlockedReason { get; set; }
}
