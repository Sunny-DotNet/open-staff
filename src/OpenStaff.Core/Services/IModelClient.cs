namespace OpenStaff.Core.Services;

/// <summary>
/// LLM 模型客户端接口 / LLM model client interface
/// </summary>
public interface IModelClient
{
    /// <summary>
    /// 发送聊天请求 / Send chat completion request
    /// </summary>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送流式聊天请求 / Send streaming chat completion request
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

public class ChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = new();
    public double Temperature { get; set; } = 0.7;
    public int? MaxTokens { get; set; }
    public List<ToolDefinition>? Tools { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // system/user/assistant/tool
    public string Content { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ToolCallId { get; set; }
}

public class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public string? FinishReason { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
    public UsageInfo? Usage { get; set; }
}

public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersSchema { get; set; } = string.Empty; // JSON Schema
}

public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty; // JSON
}

public class UsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
