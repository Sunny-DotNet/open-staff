using System.Text.Json.Serialization;

namespace OpenStaff.Agent.Vendor.Anthropic;

internal record struct AnthropicModelsResponse(
    [property: JsonPropertyName("data")] AnthropicModelData[] Data,
    [property: JsonPropertyName("first_id")] string? FirstId,
    [property: JsonPropertyName("last_id")] string? LastId,
    [property: JsonPropertyName("has_more")] bool HasMore);

internal record struct AnthropicModelData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("type")] string Type);
