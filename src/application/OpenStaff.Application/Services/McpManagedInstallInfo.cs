using System.Text.Json;

namespace OpenStaff.Application.McpServers.Services;
internal sealed class McpManagedInstallInfo
{
    public Guid? InstallId { get; init; }

    public string? CatalogEntryId { get; init; }

    public string? SourceKey { get; init; }

    public string? ChannelId { get; init; }

    public string? ChannelType { get; init; }

    public string? InstalledVersion { get; init; }

    public string? InstallState { get; init; }

    public string? InstallDirectory { get; init; }

    public string? ManifestPath { get; init; }

    public string? LastError { get; init; }

    public string? TransportType { get; init; }

    public string? Url { get; init; }

    public Dictionary<string, string?> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Command { get; init; }

    public List<string> Args { get; init; } = [];

    public Dictionary<string, string?> EnvironmentVariables { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string? WorkingDirectory { get; init; }

    public bool IsManagedInstall => InstallId.HasValue;

    public static McpManagedInstallInfo? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<McpManagedInstallInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

