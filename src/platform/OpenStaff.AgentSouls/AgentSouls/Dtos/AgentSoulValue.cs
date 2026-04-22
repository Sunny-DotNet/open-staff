using System.Text.Json.Serialization;

namespace OpenStaff.AgentSouls.Dtos;

public record struct AgentSoulValue(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("aliases")] Dictionary<string, string> Aliases);
