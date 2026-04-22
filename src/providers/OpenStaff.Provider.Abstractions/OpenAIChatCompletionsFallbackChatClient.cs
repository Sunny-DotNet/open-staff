using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace OpenStaff.Provider;

/// <summary>
/// zh-CN: 为 OpenAI Chat Completions 提供一个仅在“启用 tools 且走流式”时才触发的兼容包装。
/// 某些 OpenAI 兼容服务会在 tool_call 增量分片里返回空 type，导致 openai-dotnet 流式反序列化直接抛错。
/// 这里改为在该场景下退回一次性响应，再拆成 ChatResponseUpdate 给上层消费，避免整个运行中断。
/// </summary>
internal sealed class OpenAIChatCompletionsFallbackChatClient(
    IChatClient innerClient,
    ILogger<OpenAIChatCompletionsFallbackChatClient> logger,
    string modelId)
    : DelegatingChatClient(innerClient)
{
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => StreamAsync(messages, options, cancellationToken);

    private async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (options?.Tools is { Count: > 0 })
        {
            logger.LogWarning(
                "Downgrading OpenAI chat-completions streaming to a single response for model {Model} because some OpenAI-compatible providers emit empty tool_call types during streaming.",
                modelId);

            var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (var update in response.ToChatResponseUpdates())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
            }

            yield break;
        }

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return update;
    }
}
