using System.Text.Json.Serialization;

namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示 MCP 运行时传输类型。
/// en: Represents MCP runtime transport types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpTransportType>))]
public enum McpTransportType
{
    Stdio,
    Http,
    Sse,
    StreamableHttp
}
