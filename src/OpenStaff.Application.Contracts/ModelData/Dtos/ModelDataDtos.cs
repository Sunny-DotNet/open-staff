using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Application.Contracts.ModelData.Dtos;

public class ModelDataStatusDto
{
    public bool IsReady { get; set; }
    public DateTime? LastUpdatedUtc { get; set; }
    public string? SourceId { get; set; }
    public int VendorCount { get; set; }
}

public class ModelDataDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Reasoning { get; set; }
    public bool ToolCall { get; set; }
    public bool Attachment { get; set; }
    public long? ContextWindow { get; set; }
    public long? MaxOutput { get; set; }
    public decimal? InputPrice { get; set; }
    public decimal? OutputPrice { get; set; }
    public string? InputModalities { get; set; }
    public string? OutputModalities { get; set; }
}

public class ModelDataProviderDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ModelCount { get; set; }
}
