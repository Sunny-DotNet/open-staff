namespace OpenStaff.Mcp.Exceptions;

/// <summary>
/// zh-CN: MCP 模块统一异常基类，用于向宿主暴露稳定的错误码。
/// en: Base exception for the MCP module that exposes stable error codes to hosts.
/// </summary>
public class McpException : Exception
{
    public McpException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
