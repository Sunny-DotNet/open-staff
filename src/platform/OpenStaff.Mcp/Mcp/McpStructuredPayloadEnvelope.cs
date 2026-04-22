using System.Text.Json.Nodes;

namespace OpenStaff.Mcp;

public static class McpStructuredPayloadEnvelope
{
    private const string SchemaProperty = "schema";
    private const string SelectedProfileIdProperty = "selectedProfileId";
    private const string ParameterValuesProperty = "parameterValues";

    private const string ConfigSchema = "openstaff.mcp-config.v2";
    private const string BindingSchema = "openstaff.mcp-binding.v2";

    public static string? CreateConfigEnvelope(string? selectedProfileId, string? parameterValues)
        => CreateEnvelope(ConfigSchema, selectedProfileId, parameterValues);

    public static string? CreateBindingEnvelope(string? selectedProfileId, string? parameterValues)
        => CreateEnvelope(BindingSchema, selectedProfileId, parameterValues);

    public static bool TryParseConfigEnvelope(string? json, out StructuredPayloadEnvelope envelope)
        => TryParseEnvelope(json, ConfigSchema, out envelope);

    public static bool TryParseBindingEnvelope(string? json, out StructuredPayloadEnvelope envelope)
        => TryParseEnvelope(json, BindingSchema, out envelope);

    public static JsonObject ParseParameterValues(string? parameterValues)
        => ParseJsonObject(parameterValues);

    private static string? CreateEnvelope(string schema, string? selectedProfileId, string? parameterValues)
    {
        var parsedParameterValues = ParseJsonObject(parameterValues);
        if (string.IsNullOrWhiteSpace(selectedProfileId) && parsedParameterValues.Count == 0)
            return null;

        var envelope = new JsonObject
        {
            [SchemaProperty] = schema,
            [ParameterValuesProperty] = parsedParameterValues
        };

        if (!string.IsNullOrWhiteSpace(selectedProfileId))
            envelope[SelectedProfileIdProperty] = selectedProfileId.Trim();

        return envelope.ToJsonString();
    }

    private static bool TryParseEnvelope(string? json, string schema, out StructuredPayloadEnvelope envelope)
    {
        envelope = new StructuredPayloadEnvelope(null, new JsonObject(), false);

        if (ParseJsonObject(json) is not { } root
            || !string.Equals(ReadString(root, SchemaProperty), schema, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parameterValues = root[ParameterValuesProperty] as JsonObject ?? new JsonObject();
        envelope = new StructuredPayloadEnvelope(
            ReadString(root, SelectedProfileIdProperty),
            Clone(parameterValues),
            true);
        return true;
    }

    private static JsonObject ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static JsonObject Clone(JsonObject source)
        => JsonNode.Parse(source.ToJsonString()) as JsonObject ?? new JsonObject();

    private static string? ReadString(JsonObject node, string propertyName)
        => node[propertyName] is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : null;
}

public sealed record StructuredPayloadEnvelope(
    string? SelectedProfileId,
    JsonObject ParameterValues,
    bool IsStructured);
