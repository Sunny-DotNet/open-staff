using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace OpenStaff.Plugins.ModelDataSource;

internal class ModelsDevModelDataSource
{
}



internal record ModelsDevModel
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("family")] public string? Family { get; init; }
    [JsonPropertyName("attachment")] public bool? Attachment { get; init; }
    [JsonPropertyName("reasoning")] public bool? Reasoning { get; init; }
    [JsonPropertyName("tool_call")] public bool? ToolCall { get; init; }
    [JsonPropertyName("structured_output")] public bool? StructuredOutput { get; init; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; init; }
    [JsonPropertyName("cost")] public ModelsDevCost? Cost { get; init; }
    [JsonPropertyName("limit")] public ModelsDevLimit? Limit { get; init; }
    [JsonPropertyName("modalities")] public ModelsDevModalities? Modalities { get; init; }
}

internal record ModelsDevCost
{
    [JsonPropertyName("input")] public decimal? Input { get; init; }
    [JsonPropertyName("output")] public decimal? Output { get; init; }
    [JsonPropertyName("reasoning")] public decimal? Reasoning { get; init; }
    [JsonPropertyName("cache_read")] public decimal? CacheRead { get; init; }
    [JsonPropertyName("cache_write")] public decimal? CacheWrite { get; init; }
}

internal record ModelsDevLimit
{
    [JsonPropertyName("context")] public int? Context { get; init; }
    [JsonPropertyName("input")] public int? Input { get; init; }
    [JsonPropertyName("output")] public int? Output { get; init; }
}

internal record ModelsDevModalities
{
    [JsonPropertyName("input")] public List<string>? Input { get; init; }
    [JsonPropertyName("output")] public List<string>? Output { get; init; }
}
