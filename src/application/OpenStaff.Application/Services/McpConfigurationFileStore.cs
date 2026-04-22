using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using OpenStaff.Entities;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Options;

namespace OpenStaff.Application.McpServers.Services;

public interface IMcpConfigurationFileStore
{
    Task<McpStoredConfiguration> GetOrCreateGlobalAsync(McpServer server, CancellationToken ct = default);
    Task<McpStoredConfiguration?> GetProjectOverrideAsync(Guid serverId, string? workspacePath, CancellationToken ct = default);
    Task<McpStoredConfiguration> SaveGlobalAsync(McpServer server, string? selectedProfileId, string? parameterValues, bool isEnabled, CancellationToken ct = default);
    Task<McpStoredConfiguration> SaveProjectOverrideAsync(McpServer server, string workspacePath, string? selectedProfileId, string? parameterValues, bool isEnabled, CancellationToken ct = default);
    Task DeleteGlobalAsync(Guid serverId, CancellationToken ct = default);
    Task DeleteProjectOverrideAsync(Guid serverId, string? workspacePath, CancellationToken ct = default);
    Task DeleteProjectOverridesAsync(Guid serverId, IEnumerable<string?> workspacePaths, CancellationToken ct = default);
    McpStoredConfiguration CreateGlobalDefault(McpServer server);
    McpStoredConfiguration CreateProjectDefault(McpServer server, string? workspacePath);
}

public sealed class McpConfigurationFileStore : IMcpConfigurationFileStore
{
    private const string FileSchema = "openstaff.mcp-config-file.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly OpenStaffOptions _openStaffOptions;
    private readonly EncryptionService _encryption;
    private readonly McpRuntimeParameterDefaultsService _runtimeParameterDefaults;
    private readonly McpProfileConnectionRenderer _profileConnectionRenderer;

    public McpConfigurationFileStore(
        IOptions<OpenStaffOptions> openStaffOptions,
        EncryptionService encryption,
        McpRuntimeParameterDefaultsService runtimeParameterDefaults,
        McpProfileConnectionRenderer profileConnectionRenderer)
    {
        _openStaffOptions = openStaffOptions.Value;
        _encryption = encryption;
        _runtimeParameterDefaults = runtimeParameterDefaults;
        _profileConnectionRenderer = profileConnectionRenderer;
    }

    public async Task<McpStoredConfiguration> GetOrCreateGlobalAsync(McpServer server, CancellationToken ct = default)
    {
        var path = GetGlobalPath(server.Id);
        var existing = await ReadAsync(path, ct);
        if (existing is not null)
            return existing;

        var created = CreateGlobalDefault(server);
        return await SaveAsync(path, created, ct);
    }

    public Task<McpStoredConfiguration?> GetProjectOverrideAsync(Guid serverId, string? workspacePath, CancellationToken ct = default)
    {
        var path = GetProjectPath(serverId, workspacePath);
        return path is null
            ? Task.FromResult<McpStoredConfiguration?>(null)
            : ReadAsync(path, ct);
    }

    public Task<McpStoredConfiguration> SaveGlobalAsync(
        McpServer server,
        string? selectedProfileId,
        string? parameterValues,
        bool isEnabled,
        CancellationToken ct = default)
        => SaveAsync(
            GetGlobalPath(server.Id),
            CreateConfiguration(server, selectedProfileId, parameterValues, isEnabled, exists: true, storagePath: null, updatedAt: DateTime.UtcNow),
            ct);

    public Task<McpStoredConfiguration> SaveProjectOverrideAsync(
        McpServer server,
        string workspacePath,
        string? selectedProfileId,
        string? parameterValues,
        bool isEnabled,
        CancellationToken ct = default)
        => SaveAsync(
            GetProjectPath(server.Id, workspacePath) ?? throw new InvalidOperationException("Project workspace path is required."),
            CreateConfiguration(server, selectedProfileId, parameterValues, isEnabled, exists: true, storagePath: null, updatedAt: DateTime.UtcNow),
            ct);

    public Task DeleteGlobalAsync(Guid serverId, CancellationToken ct = default)
        => DeleteAsync(GetGlobalPath(serverId), ct);

    public Task DeleteProjectOverrideAsync(Guid serverId, string? workspacePath, CancellationToken ct = default)
        => DeleteAsync(GetProjectPath(serverId, workspacePath), ct);

    public async Task DeleteProjectOverridesAsync(Guid serverId, IEnumerable<string?> workspacePaths, CancellationToken ct = default)
    {
        foreach (var workspacePath in workspacePaths)
            await DeleteProjectOverrideAsync(serverId, workspacePath, ct);
    }

    public McpStoredConfiguration CreateGlobalDefault(McpServer server)
        => CreateConfiguration(
            server,
            selectedProfileId: null,
            _runtimeParameterDefaults.CreateHostDefaults(server).ToJsonString(),
            isEnabled: true,
            exists: false,
            storagePath: null,
            updatedAt: null);

    public McpStoredConfiguration CreateProjectDefault(McpServer server, string? workspacePath)
        => CreateConfiguration(
            server,
            selectedProfileId: null,
            _runtimeParameterDefaults.CreateProjectDefaults(server, workspacePath).ToJsonString(),
            isEnabled: true,
            exists: false,
            storagePath: null,
            updatedAt: null);

    private McpStoredConfiguration CreateConfiguration(
        McpServer server,
        string? selectedProfileId,
        string? parameterValues,
        bool isEnabled,
        bool exists,
        string? storagePath,
        DateTime? updatedAt)
    {
        var normalizedSelectedProfileId = _profileConnectionRenderer.ResolveSelectedProfileId(server, selectedProfileId);
        return new McpStoredConfiguration(
            normalizedSelectedProfileId,
            McpStructuredPayloadEnvelope.ParseParameterValues(parameterValues),
            isEnabled,
            exists,
            updatedAt,
            storagePath);
    }

    private async Task<McpStoredConfiguration?> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        var document = JsonSerializer.Deserialize<PersistedMcpConfigurationDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException($"MCP configuration file '{path}' is invalid.");
        if (!string.Equals(document.Schema, FileSchema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"MCP configuration file '{path}' uses unsupported schema '{document.Schema}'.");

        var decrypted = _encryption.Decrypt(document.EncryptedPayload);
        if (!McpStructuredPayloadEnvelope.TryParseConfigEnvelope(decrypted, out var envelope))
            throw new InvalidOperationException($"MCP configuration file '{path}' is missing the structured config envelope.");

        return new McpStoredConfiguration(
            envelope.SelectedProfileId,
            envelope.ParameterValues,
            document.IsEnabled,
            Exists: true,
            document.UpdatedAt,
            path);
    }

    private async Task<McpStoredConfiguration> SaveAsync(string path, McpStoredConfiguration configuration, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Invalid MCP config path '{path}'."));

        var envelope = McpStructuredPayloadEnvelope.CreateConfigEnvelope(
            configuration.SelectedProfileId,
            configuration.ParameterValues.ToJsonString())
            ?? """{"schema":"openstaff.mcp-config.v2","parameterValues":{}}""";
        var document = new PersistedMcpConfigurationDocument
        {
            Schema = FileSchema,
            IsEnabled = configuration.IsEnabled,
            UpdatedAt = DateTime.UtcNow,
            EncryptedPayload = _encryption.Encrypt(envelope)
        };

        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(document, JsonOptions), ct);
        File.Move(tempPath, path, overwrite: true);

        return configuration with
        {
            Exists = true,
            UpdatedAt = document.UpdatedAt,
            StoragePath = path
        };
    }

    private static Task DeleteAsync(string? path, CancellationToken _)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Task.CompletedTask;

        File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetGlobalPath(Guid serverId)
        => Path.Combine(_openStaffOptions.WorkingDirectory, ".mcp", "global", $"{serverId:N}.json");

    private static string? GetProjectPath(Guid serverId, string? workspacePath)
        => string.IsNullOrWhiteSpace(workspacePath)
            ? null
            : Path.Combine(workspacePath.Trim(), ".mcp", $"{serverId:N}.json");

    private sealed class PersistedMcpConfigurationDocument
    {
        public string Schema { get; set; } = FileSchema;
        public bool IsEnabled { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string EncryptedPayload { get; set; } = string.Empty;
    }
}

public sealed record McpStoredConfiguration(
    string? SelectedProfileId,
    JsonObject ParameterValues,
    bool IsEnabled,
    bool Exists,
    DateTime? UpdatedAt,
    string? StoragePath);
