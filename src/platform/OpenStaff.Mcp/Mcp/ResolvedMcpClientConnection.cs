namespace OpenStaff.Mcp;

public sealed record ResolvedMcpClientConnection(
    string CacheKey,
    Guid ServerId,
    string Name,
    string TransportType,
    string? ConnectionConfigJson,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables,
    string? NpmPackage = null,
    Guid? AgentRoleId = null,
    Guid? ProjectId = null,
    Guid? SessionId = null,
    string? Scene = null,
    Guid? ProjectAgentRoleId = null,
    string? DispatchSource = null);
