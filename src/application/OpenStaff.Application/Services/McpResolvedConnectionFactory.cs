using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using OpenStaff.Entities;

namespace OpenStaff.Application.McpServers.Services;

public sealed class McpResolvedConnectionFactory
{
    private readonly McpProfileConnectionRenderer _profileConnectionRenderer;
    private readonly McpRuntimeParameterDefaultsService _runtimeParameterDefaults;

    public McpResolvedConnectionFactory(
        McpProfileConnectionRenderer profileConnectionRenderer,
        McpRuntimeParameterDefaultsService runtimeParameterDefaults)
    {
        _profileConnectionRenderer = profileConnectionRenderer;
        _runtimeParameterDefaults = runtimeParameterDefaults;
    }

    public ResolvedMcpClientConnection CreateForAgentRole(
        McpServer server,
        Guid agentRoleId,
        McpStoredConfiguration configuration,
        Guid? sessionId = null,
        string? scene = null,
        string? dispatchSource = null)
        => Create(
            server,
            configuration,
            cacheKey: $"role:{agentRoleId:N}:server:{server.Id:N}",
            agentRoleId: agentRoleId,
            projectId: null,
            projectWorkspacePath: null,
            sessionId: sessionId,
            scene: scene,
            projectAgentRoleId: null,
            dispatchSource: dispatchSource);

    public ResolvedMcpClientConnection CreateForProject(
        McpServer server,
        Guid projectId,
        Guid agentRoleId,
        string? projectWorkspacePath,
        McpStoredConfiguration configuration,
        Guid? sessionId = null,
        string? scene = null,
        Guid? projectAgentRoleId = null,
        string? dispatchSource = null)
        => Create(
            server,
            configuration,
            cacheKey: $"project:{projectId:N}:role:{agentRoleId:N}:server:{server.Id:N}",
            agentRoleId: agentRoleId,
            projectId: projectId,
            projectWorkspacePath: projectWorkspacePath,
            sessionId: sessionId,
            scene: scene,
            projectAgentRoleId: projectAgentRoleId,
            dispatchSource: dispatchSource);

    public ResolvedMcpClientConnection CreateForDraft(
        McpServer server,
        string? selectedProfileId,
        string? parameterValues,
        Guid? projectId = null,
        string? projectWorkspacePath = null)
    {
        var configuration = new McpStoredConfiguration(
            _profileConnectionRenderer.ResolveSelectedProfileId(server, selectedProfileId),
            McpStructuredPayloadEnvelope.ParseParameterValues(parameterValues),
            IsEnabled: true,
            Exists: false,
            UpdatedAt: null,
            StoragePath: null);
        return Create(
            server,
            configuration,
            cacheKey: $"draft:{server.Id:N}:{CreateParameterHash(configuration)}",
            agentRoleId: null,
            projectId: projectId,
            projectWorkspacePath: projectWorkspacePath,
            sessionId: null,
            scene: null,
            projectAgentRoleId: null,
            dispatchSource: null);
    }

    public McpStoredConfiguration ApplyProjectContext(
        McpServer server,
        McpStoredConfiguration configuration,
        string? projectWorkspacePath)
        => configuration with
        {
            ParameterValues = _runtimeParameterDefaults.RewriteForProjectContext(server, configuration.ParameterValues, projectWorkspacePath)
        };

    private ResolvedMcpClientConnection Create(
        McpServer server,
        McpStoredConfiguration configuration,
        string cacheKey,
        Guid? agentRoleId,
        Guid? projectId,
        string? projectWorkspacePath,
        Guid? sessionId,
        string? scene,
        Guid? projectAgentRoleId,
        string? dispatchSource)
    {
        var effectiveConfiguration = projectId.HasValue
            ? ApplyProjectContext(server, configuration, projectWorkspacePath)
            : configuration;
        var rendered = _profileConnectionRenderer.RenderForConfig(
            server,
            effectiveConfiguration.SelectedProfileId,
            effectiveConfiguration.ParameterValues.ToJsonString());

        var transportType = rendered["transportType"]?.GetValue<string>() ?? server.TransportType;
        var environmentVariables = ExtractEnvironmentVariables(rendered);
        return new ResolvedMcpClientConnection(
            CacheKey: cacheKey,
            ServerId: server.Id,
            Name: server.Name,
            TransportType: transportType,
            ConnectionConfigJson: rendered.ToJsonString(),
            EnvironmentVariables: environmentVariables,
            NpmPackage: server.NpmPackage,
            AgentRoleId: agentRoleId,
            ProjectId: projectId,
            SessionId: sessionId,
            Scene: scene,
            ProjectAgentRoleId: projectAgentRoleId,
            DispatchSource: dispatchSource);
    }

    private static IReadOnlyDictionary<string, string?>? ExtractEnvironmentVariables(JsonObject config)
    {
        var envNode = config["env"] as JsonObject ?? config["environmentVariables"] as JsonObject;
        if (envNode == null)
            return null;

        config.Remove("env");
        config.Remove("environmentVariables");

        return envNode
            .Where(item => item.Value != null)
            .ToDictionary(
                item => item.Key,
                item => item.Value?.GetValue<string>() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateParameterHash(McpStoredConfiguration configuration)
    {
        var payload = $"{configuration.SelectedProfileId}|{configuration.ParameterValues.ToJsonString()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
