namespace OpenStaff.Mcp.Exceptions;

/// <summary>
/// zh-CN: 当运行时规格无法被可靠解析时抛出。
/// en: Thrown when the runtime specification cannot be resolved reliably.
/// </summary>
public sealed class RuntimeResolutionException : McpException
{
    public RuntimeResolutionException(string message, Exception? innerException = null)
        : base("RuntimeResolutionFailed", message, innerException)
    {
    }
}
