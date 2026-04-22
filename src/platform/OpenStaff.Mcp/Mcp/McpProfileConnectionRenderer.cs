using System.Globalization;
using System.Text.Json.Nodes;
using OpenStaff.Dtos;
using OpenStaff.Entities;

namespace OpenStaff.Mcp;

public sealed class McpProfileConnectionRenderer
{
    private readonly McpStructuredMetadataFactory _metadataFactory;

    public McpProfileConnectionRenderer(McpStructuredMetadataFactory metadataFactory)
    {
        _metadataFactory = metadataFactory;
    }

    public JsonObject RenderForConfig(
        McpServer server,
        string? selectedProfileId,
        string? parameterValues)
    {
        var metadata = _metadataFactory.Build(server);
        var selectedProfile = SelectProfile(metadata, selectedProfileId);
        var parameterObject = McpStructuredPayloadEnvelope.ParseParameterValues(parameterValues);
        var effectiveValues = BuildEffectiveParameterValues(metadata, selectedProfile?.Id, parameterObject);
        var isManagedInstall = IsManagedInstall(server);

        JsonObject merged;
        if (isManagedInstall)
        {
            merged = ParseJsonObject(server.InstallInfo);
            merged = DeepMerge(merged, Clone(parameterObject));
        }
        else if (IsStructuredTemplateDocument(server.DefaultConfig))
        {
            merged = Clone(parameterObject);
        }
        else
        {
            merged = ParseJsonObject(GetBaseServerConfig(server));
            merged = DeepMerge(merged, Clone(parameterObject));
        }

        if (selectedProfile is not null)
        {
            var overlay = RenderProfile(selectedProfile, effectiveValues);
            MergeProfileOverlay(merged, overlay, preserveInstalledRuntime: isManagedInstall);
        }

        ApplyKnownServerRuntimeParameters(server, merged);
        return merged;
    }

    public JsonObject RenderForBinding(
        McpServer server,
        string? selectedProfileId,
        string? parameterValues)
    {
        var metadata = _metadataFactory.Build(server);
        var selectedProfile = SelectProfile(metadata, selectedProfileId);
        var parameterObject = McpStructuredPayloadEnvelope.ParseParameterValues(parameterValues);
        var effectiveValues = BuildEffectiveParameterValues(metadata, selectedProfile?.Id, parameterObject);

        var merged = ParseJsonObject(GetBaseServerConfig(server));
        merged = DeepMerge(merged, Clone(parameterObject));

        if (selectedProfile is not null)
        {
            var overlay = RenderProfile(selectedProfile, effectiveValues);
            MergeProfileOverlay(merged, overlay, preserveInstalledRuntime: IsManagedInstall(server));
        }

        ApplyKnownServerRuntimeParameters(server, merged);
        return merged;
    }

    public string? ResolveSelectedProfileId(McpServer server, string? selectedProfileId)
        => SelectProfile(_metadataFactory.Build(server), selectedProfileId)?.Id;

    public string ResolveTransportType(McpServer server, string? selectedProfileId, string? fallbackTransportType = null)
        => ResolveSelectedProfileId(server, selectedProfileId) is { } resolvedProfileId
            ? _metadataFactory.Build(server).Profiles
                .First(profile => string.Equals(profile.Id, resolvedProfileId, StringComparison.OrdinalIgnoreCase))
                .TransportType
            : fallbackTransportType ?? server.TransportType;

    private static McpLaunchProfileDto? SelectProfile(StructuredMcpServerMetadata metadata, string? selectedProfileId)
    {
        var profile = metadata.Profiles.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(selectedProfileId)
            && string.Equals(item.Id, selectedProfileId, StringComparison.OrdinalIgnoreCase));

        if (profile is not null)
            return profile;

        if (!string.IsNullOrWhiteSpace(metadata.DefaultProfileId))
        {
            profile = metadata.Profiles.FirstOrDefault(item =>
                string.Equals(item.Id, metadata.DefaultProfileId, StringComparison.OrdinalIgnoreCase));
            if (profile is not null)
                return profile;
        }

        return metadata.Profiles.FirstOrDefault();
    }

    private static JsonObject BuildEffectiveParameterValues(
        StructuredMcpServerMetadata metadata,
        string? selectedProfileId,
        JsonObject providedValues)
    {
        var result = new JsonObject();

        foreach (var item in metadata.ParameterSchema)
        {
            if (!AppliesToProfile(item, selectedProfileId))
                continue;

            if (item.DefaultValue is not null)
                result[item.Key] = JsonValueFor(item.DefaultValue);
        }

        return DeepMerge(result, Clone(providedValues));
    }

    private static bool AppliesToProfile(McpParameterSchemaItemDto item, string? selectedProfileId)
    {
        if (item.AppliesToProfiles.Count == 0 || string.IsNullOrWhiteSpace(selectedProfileId))
            return true;

        return item.AppliesToProfiles.Contains(selectedProfileId, StringComparer.OrdinalIgnoreCase);
    }

    private static JsonObject RenderProfile(McpLaunchProfileDto profile, JsonObject parameterValues)
    {
        var rendered = new JsonObject
        {
            ["transportType"] = profile.TransportType
        };

        var command = !string.IsNullOrWhiteSpace(profile.Command)
            ? ResolveString(profile.Command, parameterValues)
            : ResolveString(profile.CommandTemplate, parameterValues);
        var args = profile.ArgsTemplate
            .Select(argument => ResolveString(argument, parameterValues))
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToList();
        var env = ResolveDictionary(profile.EnvTemplate, parameterValues);
        var headers = ResolveDictionary(profile.HeadersTemplate, parameterValues);
        var workingDirectory = ResolveString(profile.WorkingDirectoryTemplate, parameterValues);
        var url = ResolveString(profile.UrlTemplate, parameterValues);

        if (!string.IsNullOrWhiteSpace(command))
            rendered["command"] = command;
        if (args.Count > 0)
            rendered["args"] = new JsonArray(args.Select(argument => (JsonNode?)JsonValue.Create(argument)).ToArray());
        if (env.Count > 0)
            rendered["env"] = ToJsonObject(env);
        if (headers.Count > 0)
            rendered["headers"] = ToJsonObject(headers);
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            rendered["workingDirectory"] = workingDirectory;
        if (!string.IsNullOrWhiteSpace(url))
            rendered["url"] = url;

        return rendered;
    }

    private static Dictionary<string, string?> ResolveDictionary(
        IReadOnlyDictionary<string, string?> templates,
        JsonObject parameterValues)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in templates)
        {
            var resolved = ResolveString(pair.Value, parameterValues);
            if (!string.IsNullOrWhiteSpace(resolved))
                result[pair.Key] = resolved;
        }

        return result;
    }

    private static string? ResolveString(string? template, JsonObject parameterValues)
    {
        if (string.IsNullOrWhiteSpace(template))
            return template;

        var current = template;
        while (true)
        {
            var start = current.IndexOf("${param:", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                break;

            var end = current.IndexOf('}', start);
            if (end < 0)
                break;

            var key = current[(start + "${param:".Length)..end];
            if (!parameterValues.TryGetPropertyValue(key, out var node))
                return null;

            var replacement = ConvertNodeToString(node);
            current = $"{current[..start]}{replacement}{current[(end + 1)..]}";
        }

        return current;
    }

    private static string ConvertNodeToString(JsonNode? node)
    {
        return node switch
        {
            null => string.Empty,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            JsonValue value when value.TryGetValue<int>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<long>(out var longValue) => longValue.ToString(CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<double>(out var doubleValue) => doubleValue.ToString(CultureInfo.InvariantCulture),
            _ => node.ToJsonString()
        };
    }

    private static void MergeProfileOverlay(JsonObject target, JsonObject overlay, bool preserveInstalledRuntime)
    {
        if (overlay["transportType"] is JsonNode transportType)
            target["transportType"] = transportType.DeepClone();

        if (overlay["url"] is JsonNode urlNode)
            target["url"] = urlNode.DeepClone();

        if (overlay["workingDirectory"] is JsonNode workingDirectoryNode)
            target["workingDirectory"] = workingDirectoryNode.DeepClone();

        if (overlay["headers"] is JsonObject overlayHeaders)
        {
            var headers = target["headers"] as JsonObject ?? new JsonObject();
            target["headers"] = DeepMerge(headers, Clone(overlayHeaders));
        }

        if (overlay["env"] is JsonObject overlayEnv)
        {
            var env = target["env"] as JsonObject ?? new JsonObject();
            target["env"] = DeepMerge(env, Clone(overlayEnv));
        }

        if (!preserveInstalledRuntime)
        {
            if (overlay["command"] is JsonNode commandNode)
                target["command"] = commandNode.DeepClone();
            if (overlay["args"] is JsonNode argsNode)
                target["args"] = argsNode.DeepClone();
        }
    }

    private static string? GetBaseServerConfig(McpServer server)
    {
        return !string.IsNullOrWhiteSpace(server.InstallInfo)
            ? server.InstallInfo
            : server.DefaultConfig;
    }

    private static bool IsManagedInstall(McpServer server)
    {
        var installInfo = ParseJsonObject(server.InstallInfo);
        return installInfo.TryGetPropertyValue("installId", out var installIdNode)
            && !string.IsNullOrWhiteSpace(ConvertNodeToString(installIdNode));
    }

    private static bool IsStructuredTemplateDocument(string? json)
        => ParseJsonObject(json) is { } parsed
            && parsed.TryGetPropertyValue("schema", out var node)
            && string.Equals(ConvertNodeToString(node), "openstaff.mcp-template.v1", StringComparison.OrdinalIgnoreCase);

    private static void ApplyKnownServerRuntimeParameters(McpServer server, JsonObject config)
    {
        if (!IsFilesystemServer(server))
            return;

        var workspacePath = GetFilesystemWorkspacePath(config);
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        var existingArgs = (config["args"] as JsonArray)?
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToList()
            ?? [];

        var rebuiltArgs = new List<string>();
        foreach (var arg in existingArgs)
        {
            if (IsWorkspacePlaceholder(arg))
            {
                rebuiltArgs.Add(workspacePath);
                continue;
            }

            rebuiltArgs.Add(arg);
        }

        config["args"] = new JsonArray(rebuiltArgs.Select(arg => (JsonNode?)JsonValue.Create(arg)).ToArray());
        if (IsFilesystemPlaceholderValue(config["workingDirectory"]))
            config["workingDirectory"] = workspacePath;
        if (IsFilesystemPlaceholderValue(config["workspacePath"]))
            config["workspacePath"] = workspacePath;
        config.Remove("workspace");
        config.Remove("workspaces");
    }

    private static string? GetFilesystemWorkspacePath(JsonObject config)
        => config["workspacePath"] is JsonValue workspacePathValue
           && workspacePathValue.TryGetValue<string>(out var workspacePath)
           && !string.IsNullOrWhiteSpace(workspacePath)
           && !IsWorkspacePlaceholder(workspacePath.Trim())
            ? workspacePath.Trim()
            : null;

    private static bool IsWorkspacePlaceholder(string arg)
        => string.Equals(arg, "/path/to/workspace", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "{workspace}", StringComparison.OrdinalIgnoreCase)
            || IsProjectWorkspacePlaceholder(arg);

    private static bool IsFilesystemPlaceholderValue(JsonNode? node)
        => node is null
            || node is JsonValue value
               && value.TryGetValue<string>(out var text)
               && (string.IsNullOrWhiteSpace(text) || IsWorkspacePlaceholder(text.Trim()) || IsProjectWorkspacePlaceholder(text.Trim()));

    private static bool IsProjectWorkspacePlaceholder(string value)
        => string.Equals(value, "${project.workspace}", StringComparison.OrdinalIgnoreCase);

    private static bool IsFilesystemServer(McpServer server)
        => string.Equals(server.Name, "Filesystem", StringComparison.OrdinalIgnoreCase)
            || string.Equals(server.NpmPackage, "@modelcontextprotocol/server-filesystem", StringComparison.OrdinalIgnoreCase);

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

    private static JsonObject DeepMerge(JsonObject target, JsonObject overlay)
    {
        foreach (var pair in overlay)
        {
            if (pair.Value is JsonObject overlayObject)
            {
                var targetObject = target[pair.Key] as JsonObject ?? new JsonObject();
                target[pair.Key] = DeepMerge(targetObject, Clone(overlayObject));
            }
            else
            {
                target[pair.Key] = pair.Value?.DeepClone();
            }
        }

        return target;
    }

    private static JsonObject ToJsonObject(IReadOnlyDictionary<string, string?> values)
    {
        var result = new JsonObject();
        foreach (var pair in values)
            result[pair.Key] = pair.Value;
        return result;
    }

    private static JsonNode? JsonValueFor(object value)
    {
        return value switch
        {
            null => null,
            bool boolean => JsonValue.Create(boolean),
            int integer => JsonValue.Create(integer),
            long longValue => JsonValue.Create(longValue),
            double doubleValue => JsonValue.Create(doubleValue),
            string text => JsonValue.Create(text),
            _ => JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(value))
        };
    }
}
