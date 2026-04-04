using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Services;

namespace OpenStaff.Infrastructure.LLM;

/// <summary>
/// OpenAI 兼容客户端 / OpenAI-compatible client
/// 支持 OpenAI、Azure OpenAI、国内厂商等兼容接口
/// </summary>
public class OpenAICompatibleClient : IModelClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<OpenAICompatibleClient> _logger;

    public OpenAICompatibleClient(HttpClient httpClient, string apiKey, string baseUrl, ILogger<OpenAICompatibleClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/v1/chat/completions";
        var payload = BuildPayload(request, stream: false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(cancellationToken: cancellationToken);

        return new ChatResponse
        {
            Content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty,
            FinishReason = result?.Choices?.FirstOrDefault()?.FinishReason,
            Usage = result?.Usage != null ? new UsageInfo
            {
                PromptTokens = result.Usage.PromptTokens,
                CompletionTokens = result.Usage.CompletionTokens,
                TotalTokens = result.Usage.TotalTokens
            } : null
        };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/v1/chat/completions";
        var payload = BuildPayload(request, stream: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            var chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data);
            var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(content))
                yield return content;
        }
    }

    private static object BuildPayload(ChatRequest request, bool stream)
    {
        return new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content, name = m.Name }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = stream
        };
    }

    // OpenAI API response models
    private class OpenAIChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
        [JsonPropertyName("usage")] public Usage? Usage { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")] public MessageContent? Message { get; set; }
        [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
    }

    private class MessageContent
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }

    private class OpenAIStreamChunk
    {
        [JsonPropertyName("choices")] public List<StreamChoice>? Choices { get; set; }
    }

    private class StreamChoice
    {
        [JsonPropertyName("delta")] public DeltaContent? Delta { get; set; }
    }

    private class DeltaContent
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
