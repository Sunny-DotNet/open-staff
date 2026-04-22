namespace OpenStaff.Mcp.Exceptions;

/// <summary>
/// zh-CN: 当卸载被引用关系阻塞时抛出。
/// en: Thrown when uninstall is blocked by active references.
/// </summary>
public sealed class UninstallBlockedException : McpException
{
    public UninstallBlockedException(string message)
        : base("UninstallBlocked", message)
    {
    }
}
