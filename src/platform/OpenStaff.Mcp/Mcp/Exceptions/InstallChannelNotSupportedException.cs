using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Exceptions;

/// <summary>
/// zh-CN: 当安装通道没有对应安装器时抛出。
/// en: Thrown when no installer supports the selected channel.
/// </summary>
public sealed class InstallChannelNotSupportedException : McpException
{
    public InstallChannelNotSupportedException(McpChannelType channelType)
        : base("InstallChannelNotSupported", $"Install channel '{channelType}' is not supported.")
    {
    }
}
