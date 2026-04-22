using System.Text.Json.Serialization;

namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示 MCP 支持的安装通道类型。
/// en: Represents the installation-channel types supported by the MCP module.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpChannelType>))]
public enum McpChannelType
{
    Remote,
    Npm,
    Pypi,
    GithubRelease,
    Zip
}

/// <summary>
/// zh-CN: 将安装通道类型映射到稳定的存储值。
/// en: Maps channel types to stable storage values.
/// </summary>
public static class McpChannelTypeExtensions
{
    public static string ToStorageValue(this McpChannelType channelType) => channelType switch
    {
        McpChannelType.Remote => "remote",
        McpChannelType.Npm => "npm",
        McpChannelType.Pypi => "pypi",
        McpChannelType.GithubRelease => "github-release",
        McpChannelType.Zip => "zip",
        _ => throw new ArgumentOutOfRangeException(nameof(channelType), channelType, null)
    };
}
