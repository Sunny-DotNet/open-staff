using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using OpenStaff.Provider.Models;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// OpenAI 兼容聊天客户端辅助方法，封装 endpoint 规范化、协议顺序解释和运行时客户端创建逻辑。
/// OpenAI-compatible chat-client helpers that encapsulate endpoint normalization, protocol-order interpretation, and runtime client creation.
/// </summary>
public static class OpenAICompatibleChatClientFactorySupport
{
    /// <summary>
    /// 默认网络超时，避免长响应模型过早被 30 秒短超时中断。
    /// Default network timeout used to prevent long-running models from failing under a short 30-second timeout.
    /// </summary>
    public static readonly TimeSpan DefaultNetworkTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 在有序协议列表中选择当前应使用的 OpenAI 兼容运行时；仅在 provider 没给出顺序时回退到模型名启发式。
    /// Selects the OpenAI-compatible runtime from the ordered protocol list and falls back to model-name heuristics only when the provider supplied no order.
    /// </summary>
    /// <param name="preferredProtocols">
    /// provider 声明的协议优先级顺序。
    /// Provider-defined protocol priority order.
    /// </param>
    /// <param name="model">
    /// 目标模型标识。
    /// Target model identifier.
    /// </param>
    /// <returns>
    /// 应使用的 OpenAI 兼容运行时。
    /// OpenAI-compatible runtime that should be used.
    /// </returns>
    public static OpenAICompatibleRuntime ResolveOpenAICompatibleRuntime(
        IReadOnlyList<ModelProtocolType>? preferredProtocols,
        string model)
    {
        if (preferredProtocols is { Count: > 0 })
        {
            foreach (var protocol in preferredProtocols)
            {
                if (protocol == ModelProtocolType.OpenAIChatCompletions)
                    return OpenAICompatibleRuntime.ChatCompletions;

                if (protocol == ModelProtocolType.OpenAIResponse)
                    return OpenAICompatibleRuntime.Responses;
            }
        }

        return RequiresResponsesApi(model)
            ? OpenAICompatibleRuntime.Responses
            : OpenAICompatibleRuntime.ChatCompletions;
    }

    /// <summary>
    /// 确认模型元数据中至少声明了一种当前 builtin 运行时支持的 OpenAI 兼容协议。
    /// Confirms the model metadata declares at least one OpenAI-compatible protocol supported by the builtin runtime.
    /// </summary>
    /// <param name="providerProtocolType">
    /// provider 协议键。
    /// Provider protocol key.
    /// </param>
    /// <param name="model">
    /// 目标模型标识。
    /// Target model identifier.
    /// </param>
    /// <param name="modelInfo">
    /// 解析到的模型元数据。
    /// Resolved model metadata.
    /// </param>
    /// <param name="preferredProtocols">
    /// provider 声明的协议优先级顺序。
    /// Provider-defined protocol priority order.
    /// </param>
    public static void EnsureOpenAICompatibleRuntime(
        string providerProtocolType,
        string model,
        ModelInfo? modelInfo,
        IReadOnlyList<ModelProtocolType> preferredProtocols)
    {
        if (SupportsOpenAICompatibleRuntime(preferredProtocols))
            return;

        var protocols = modelInfo?.ModelProtocols ?? ModelProtocolType.None;
        if (protocols == ModelProtocolType.None || SupportsOpenAICompatibleRuntime(protocols))
            return;

        throw new NotSupportedException(
            $"Model '{model}' on provider '{providerProtocolType}' resolved to '{protocols}' " +
            $"with preferred protocols [{string.Join(", ", preferredProtocols)}], " +
            "but the builtin chat runtime currently supports only OpenAI-compatible protocols.");
    }

    /// <summary>
    /// 使用共享 OpenAI 选项和协议顺序创建 OpenAI 兼容聊天客户端。
    /// Creates an OpenAI-compatible chat client using shared OpenAI options and protocol order.
    /// </summary>
    /// <param name="apiKey">
    /// API 密钥。
    /// API key.
    /// </param>
    /// <param name="model">
    /// 目标模型标识。
    /// Target model identifier.
    /// </param>
    /// <param name="preferredProtocols">
    /// provider 声明的协议优先级顺序。
    /// Provider-defined protocol priority order.
    /// </param>
    /// <param name="options">
    /// OpenAI 客户端选项。
    /// OpenAI client options.
    /// </param>
    /// <returns>
    /// OpenAI 兼容聊天客户端。
    /// OpenAI-compatible chat client.
    /// </returns>
    public static IChatClient CreateOpenAICompatibleChatClient(
        string apiKey,
        string model,
        IReadOnlyList<ModelProtocolType>? preferredProtocols,
        OpenAIClientOptions? options = null)
    {
        options ??= CreateOpenAIClientOptions(null);
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);

#pragma warning disable OPENAI001
        if (ResolveOpenAICompatibleRuntime(preferredProtocols, model) == OpenAICompatibleRuntime.Responses)
            return client.GetResponsesClient().AsIChatClient(model);
#pragma warning restore OPENAI001

        return client.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// 创建 OpenAI 客户端选项，并统一设置网络超时和可选 endpoint 覆盖。
    /// Creates OpenAI client options with a consistent network timeout and an optional endpoint override.
    /// </summary>
    /// <param name="endpoint">
    /// 需要覆盖的服务 endpoint。
    /// Optional service endpoint override.
    /// </param>
    /// <returns>
    /// OpenAI 客户端选项。
    /// OpenAI client options.
    /// </returns>
    public static OpenAIClientOptions CreateOpenAIClientOptions(string? endpoint)
    {
        var options = new OpenAIClientOptions
        {
            NetworkTimeout = DefaultNetworkTimeout
        };

        if (!string.IsNullOrWhiteSpace(endpoint))
            options.Endpoint = new Uri(endpoint);

        return options;
    }

    /// <summary>
    /// 给 Copilot 请求附加其上游代理要求的固定头信息。
    /// Adds the fixed headers required by the Copilot upstream proxy.
    /// </summary>
    /// <param name="options">
    /// 需要补充策略的 OpenAI 客户端选项。
    /// OpenAI client options to augment with the Copilot policy.
    /// </param>
    public static void ConfigureCopilotOptions(OpenAIClientOptions options)
    {
        options.AddPolicy(new CopilotHeaderPolicy(), PipelinePosition.PerCall);
    }

    /// <summary>
    /// 规范化 OpenAI 兼容基础地址，确保 SDK 始终看到以 <c>/v1</c> 结尾的服务根。
    /// Normalizes OpenAI-compatible base URLs so the SDK always sees a service root ending in <c>/v1</c>.
    /// </summary>
    /// <param name="baseUrl">
    /// provider 返回的基础地址。
    /// Base URL returned by the provider.
    /// </param>
    /// <returns>
    /// 规范化后的基础地址。
    /// Normalized base URL.
    /// </returns>
    public static string? NormalizeOpenAIBaseUrl(string? baseUrl,bool endsNotWithV1=false)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var uri = baseUrl.TrimEnd('/');
        if (!endsNotWithV1&&!uri.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            uri += "/v1";

        return uri;
    }

    /// <summary>
    /// 将 Copilot 保存的完整 endpoint 裁剪回服务根，避免 SDK 再次拼接出双路径。
    /// Trims Copilot full endpoints back to the service root so the SDK does not append duplicate path segments.
    /// </summary>
    /// <param name="baseUrl">
    /// provider 保存的基础地址或完整 endpoint。
    /// Base URL or full endpoint stored by the provider.
    /// </param>
    /// <returns>
    /// Copilot 服务根地址。
    /// Copilot service root.
    /// </returns>
    public static string NormalizeCopilotBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "https://api.individual.githubcopilot.com";

        var normalized = baseUrl.TrimEnd('/');
        normalized = RemoveEndpointSuffix(normalized, "/v1/chat/completions");
        normalized = RemoveEndpointSuffix(normalized, "/chat/completions");
        normalized = RemoveEndpointSuffix(normalized, "/v1/responses");
        normalized = RemoveEndpointSuffix(normalized, "/responses");

        return normalized;
    }

    /// <summary>
    /// 标记在缺少显式顺序时仍应优先走 Responses API 的模型族。
    /// Identifies model families that should still prefer the Responses API when no explicit order is available.
    /// </summary>
    /// <param name="model">
    /// 目标模型标识。
    /// Target model identifier.
    /// </param>
    /// <returns>
    /// 如果该模型应优先走 Responses API 则返回 <see langword="true" />。
    /// <see langword="true" /> when the model should prefer the Responses API.
    /// </returns>
    public static bool RequiresResponsesApi(string model)
    {
        return model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断协议位标志是否包含 builtin 运行时支持的 OpenAI 兼容协议。
    /// Determines whether the protocol flags include an OpenAI-compatible protocol supported by the builtin runtime.
    /// </summary>
    /// <param name="protocols">
    /// 协议位标志。
    /// Protocol flags.
    /// </param>
    /// <returns>
    /// 若包含 OpenAI 兼容协议则返回 <see langword="true" />。
    /// <see langword="true" /> when an OpenAI-compatible protocol is present.
    /// </returns>
    public static bool SupportsOpenAICompatibleRuntime(ModelProtocolType protocols)
    {
        return protocols == ModelProtocolType.None
            || protocols.HasFlag(ModelProtocolType.OpenAIChatCompletions)
            || protocols.HasFlag(ModelProtocolType.OpenAIResponse);
    }

    /// <summary>
    /// 判断有序协议列表里是否至少包含一种 OpenAI 兼容协议。
    /// Determines whether the ordered protocol list contains at least one OpenAI-compatible protocol.
    /// </summary>
    /// <param name="protocols">
    /// provider 声明的协议优先级顺序。
    /// Provider-defined protocol priority order.
    /// </param>
    /// <returns>
    /// 若列表中存在 OpenAI 兼容协议则返回 <see langword="true" />。
    /// <see langword="true" /> when the list contains an OpenAI-compatible protocol.
    /// </returns>
    public static bool SupportsOpenAICompatibleRuntime(IReadOnlyList<ModelProtocolType> protocols)
    {
        return protocols.Any(protocol =>
            protocol == ModelProtocolType.OpenAIChatCompletions
            || protocol == ModelProtocolType.OpenAIResponse);
    }

    /// <summary>
    /// 在 URL 已包含指定终结点后缀时将其移除，其余情况保持原值不变。
    /// Removes a known endpoint suffix when the URL already contains it, leaving all other values unchanged.
    /// </summary>
    /// <param name="value">
    /// 待处理的 URL。
    /// URL to process.
    /// </param>
    /// <param name="suffix">
    /// 需要移除的已知终结点后缀。
    /// Known endpoint suffix to trim.
    /// </param>
    /// <returns>
    /// 裁剪后的 URL。
    /// Trimmed URL.
    /// </returns>
    private static string RemoveEndpointSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    /// <summary>
    /// OpenAI 兼容运行时类型。
    /// OpenAI-compatible runtime kinds.
    /// </summary>
    public enum OpenAICompatibleRuntime
    {
        /// <summary>
        /// 传统 Chat Completions 运行时。
        /// Legacy Chat Completions runtime.
        /// </summary>
        ChatCompletions,

        /// <summary>
        /// 新版 Responses 运行时。
        /// Newer Responses runtime.
        /// </summary>
        Responses
    }

    /// <summary>
    /// 为 GitHub Copilot 请求补充其上游代理要求的固定头信息。
    /// Adds the fixed headers required by the GitHub Copilot upstream proxy to outgoing requests.
    /// </summary>
    private sealed class CopilotHeaderPolicy : PipelinePolicy
    {
        /// <summary>
        /// 在同步请求进入下一层管线前写入 Copilot 头信息。
        /// Writes Copilot headers before passing synchronous requests to the next pipeline stage.
        /// </summary>
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddCopilotHeaders(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        /// <summary>
        /// 在异步请求进入下一层管线前写入 Copilot 头信息。
        /// Writes Copilot headers before passing asynchronous requests to the next pipeline stage.
        /// </summary>
        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddCopilotHeaders(message);
            await ProcessNextAsync(message, pipeline, currentIndex);
        }

        /// <summary>
        /// 设置 Copilot 用于鉴别客户端版本的请求头，避免上游服务拒绝请求。
        /// Sets the request headers Copilot uses to identify client version metadata so the upstream service does not reject the call.
        /// </summary>
        /// <param name="message">
        /// 当前管线消息。
        /// Current pipeline message.
        /// </param>
        private static void AddCopilotHeaders(PipelineMessage message)
        {
            var headers = message.Request.Headers;
            headers.Set("Editor-Version", "vscode/1.96.2");
            headers.Set("X-Github-Api-Version", "2025-04-01");
            headers.Set("User-Agent", "GitHubCopilotChat/1.0.102");
        }
    }
}
