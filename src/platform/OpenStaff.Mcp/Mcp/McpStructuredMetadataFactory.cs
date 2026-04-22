using System.Text.Json;
using System.Text.Json.Nodes;
using OpenStaff.Dtos;
using OpenStaff.Entities;

namespace OpenStaff.Mcp;

/// <summary>
/// Parses the structured MCP template metadata document when a server provides one.
/// Non-template servers intentionally fall back to an empty metadata shape.
/// </summary>
public sealed class McpStructuredMetadataFactory
{
    public StructuredMcpServerMetadata Build(McpServer server)
    {
        var root = ParseJsonObject(server.DefaultConfig);
        if (root is null
            || !string.Equals(ReadString(root, "schema"), "openstaff.mcp-template.v1", StringComparison.OrdinalIgnoreCase))
        {
            return new StructuredMcpServerMetadata(
                Logo: ResolveLogo(server),
                DefaultProfileId: null,
                Profiles: [],
                ParameterSchema: []);
        }

        var profiles = root["profiles"] is JsonArray profileArray
            ? profileArray
                .OfType<JsonObject>()
                .Select(ToTemplateProfile)
                .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
                .ToList()
            : [];
        var parameterSchema = root["parameter_schema"] is JsonArray schemaArray
            ? schemaArray
                .OfType<JsonObject>()
                .Select(ToTemplateParameter)
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToList()
            : [];

        return new StructuredMcpServerMetadata(
            Logo: ReadString(root, "logo") ?? ResolveLogo(server),
            DefaultProfileId: ReadString(root, "default_profile_id") ?? profiles.FirstOrDefault()?.Id,
            Profiles: profiles,
            ParameterSchema: parameterSchema);
    }

    private static McpLaunchProfileDto ToTemplateProfile(JsonObject profile)
    {
        return new McpLaunchProfileDto
        {
            Id = ReadString(profile, "id") ?? string.Empty,
            DisplayName = ReadString(profile, "display_name") ?? ReadString(profile, "id") ?? string.Empty,
            ProfileType = ReadString(profile, "profile_type") ?? "advanced-legacy",
            TransportType = ReadString(profile, "transport_type") ?? McpTransportTypes.Stdio,
            RunnerKind = ReadString(profile, "runner_kind") ?? "manual",
            Runner = ReadString(profile, "runner"),
            Ecosystem = ReadString(profile, "ecosystem"),
            PackageName = ReadString(profile, "package_name"),
            PackageVersion = ReadString(profile, "package_version"),
            Command = ReadString(profile, "command"),
            CommandTemplate = ReadString(profile, "command_template"),
            ArgsTemplate = ReadStringArray(profile, "args_template"),
            UrlTemplate = ReadString(profile, "url_template"),
            WorkingDirectoryTemplate = ReadString(profile, "working_directory_template"),
            EnvTemplate = ReadStringDictionary(profile, "env_template"),
            HeadersTemplate = ReadStringDictionary(profile, "headers_template")
        };
    }

    private static McpParameterSchemaItemDto ToTemplateParameter(JsonObject parameter)
    {
        var defaultValue = ReadString(parameter, "default_value");
        var projectOverrideValueSource = ReadString(parameter, "project_override_value_source");
        if (string.Equals(defaultValue, "${project.workspace}", StringComparison.OrdinalIgnoreCase))
        {
            defaultValue = null;
            projectOverrideValueSource ??= "project-workspace";
        }

        return new McpParameterSchemaItemDto
        {
            Key = ReadString(parameter, "key") ?? string.Empty,
            Label = ReadString(parameter, "label") ?? ReadString(parameter, "key") ?? string.Empty,
            Type = ReadString(parameter, "type") ?? "string",
            Required = ReadBoolean(parameter, "required"),
            DefaultValue = defaultValue,
            DefaultValueSource = ReadString(parameter, "default_value_source"),
            ProjectOverrideValueSource = projectOverrideValueSource,
            Description = ReadString(parameter, "description"),
            AppliesToProfiles = ReadStringArray(parameter, "applies_to_profiles")
        };
    }

    private static JsonObject? ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolveLogo(McpServer server)
    {
        if (!string.IsNullOrWhiteSpace(server.Icon))
            return server.Icon;
        return server.Name;
    }

    private static string? ReadString(JsonObject node, string propertyName)
        => node[propertyName] is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : null;

    private static bool ReadBoolean(JsonObject node, string propertyName)
        => node[propertyName] is JsonValue value
           && (value.TryGetValue<bool>(out var boolean)
               ? boolean
               : bool.TryParse(value.ToJsonString().Trim('"'), out var parsed) && parsed);

    private static List<string> ReadStringArray(JsonObject node, string propertyName)
        => node[propertyName] is JsonArray array
            ? array
                .OfType<JsonValue>()
                .Select(value => value.TryGetValue<string>(out var result) ? result : null)
                .Where(result => !string.IsNullOrWhiteSpace(result))
                .Cast<string>()
                .ToList()
            : [];

    private static Dictionary<string, string?> ReadStringDictionary(JsonObject node, string propertyName)
        => node[propertyName] is JsonObject objectNode
            ? objectNode.ToDictionary(
                pair => pair.Key,
                pair => pair.Value is JsonValue value && value.TryGetValue<string>(out var result) ? result : pair.Value?.ToJsonString(),
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

public sealed record StructuredMcpServerMetadata(
    string Logo,
    string? DefaultProfileId,
    IReadOnlyList<McpLaunchProfileDto> Profiles,
    IReadOnlyList<McpParameterSchemaItemDto> ParameterSchema);
