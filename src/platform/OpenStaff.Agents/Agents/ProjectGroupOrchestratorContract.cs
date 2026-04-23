using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace OpenStaff.Core.Agents;

/// <summary>
/// Defines the single structured result contract emitted by the hidden ProjectGroup orchestrator.
/// </summary>
public static class ProjectGroupOrchestratorContract
{
    public const string EnvelopeTag = "openstaff_project_orchestrator_result";
    public const string OutputModeExtraConfigKey = "openstaff_project_orchestrator_output_mode";
    public const string NativeJsonSchemaOutputMode = "json_schema";
    public const string TaggedJsonFallbackOutputMode = "tagged_json_fallback";

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public static bool TryParse(string? content, out ProjectGroupOrchestratorResult? result)
    {
        result = null;

        if (!TryExtractJson(content, out var json))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<ProjectGroupOrchestratorResult>(json, SerializerOptions);
            if (parsed == null)
                return false;

            var normalized = Normalize(parsed);
            if (!IsValid(normalized))
                return false;

            result = normalized;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryExtractJson(string? content, out string json)
    {
        json = string.Empty;
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var trimmed = content.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            json = trimmed;
            return true;
        }

        var openTag = $"<{EnvelopeTag}>";
        var closeTag = $"</{EnvelopeTag}>";
        var start = trimmed.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return false;

        start += openTag.Length;
        var end = trimmed.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            return false;

        json = trimmed[start..end].Trim();
        return !string.IsNullOrWhiteSpace(json);
    }

    private static ProjectGroupOrchestratorResult Normalize(ProjectGroupOrchestratorResult result)
    {
        var dispatches = (result.Dispatches ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.TargetRole) && !string.IsNullOrWhiteSpace(item.Task))
            .Select(item => new ProjectGroupOrchestratorDispatch
            {
                TargetRole = item.TargetRole.Trim(),
                Task = item.Task.Trim()
            })
            .ToArray();

        return new ProjectGroupOrchestratorResult
        {
            ReplyMode = result.ReplyMode,
            SecretaryReply = string.IsNullOrWhiteSpace(result.SecretaryReply) ? null : result.SecretaryReply.Trim(),
            Dispatches = dispatches
        };
    }

    private static bool IsValid(ProjectGroupOrchestratorResult result)
    {
        var hasSecretaryReply = !string.IsNullOrWhiteSpace(result.SecretaryReply);
        var hasDispatches = result.Dispatches.Count > 0;

        return result.ReplyMode switch
        {
            ProjectGroupOrchestratorReplyMode.SecretaryReply => hasSecretaryReply,
            ProjectGroupOrchestratorReplyMode.DispatchOnly => hasDispatches,
            ProjectGroupOrchestratorReplyMode.SecretaryReplyAndDispatch => hasSecretaryReply && hasDispatches,
            _ => false
        };
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}

public sealed class ProjectGroupOrchestratorResult
{
    public ProjectGroupOrchestratorReplyMode ReplyMode { get; init; }

    public string? SecretaryReply { get; init; }

    public IReadOnlyList<ProjectGroupOrchestratorDispatch> Dispatches { get; init; } = [];
}

public sealed class ProjectGroupOrchestratorDispatch
{
    public string TargetRole { get; init; } = string.Empty;

    public string Task { get; init; } = string.Empty;
}

public enum ProjectGroupOrchestratorReplyMode
{
    SecretaryReply,
    DispatchOnly,
    SecretaryReplyAndDispatch
}
