namespace OpenStaff.Provider.Models;

/// <summary>
/// 模型标识信息，描述一个模型及其支持的协议集合。
/// Identifies a model together with the protocol set it supports.
/// </summary>
public record class ModelInfo
{
    /// <summary>
    /// 使用模型标识、供应商标识和协议集合初始化模型信息。
    /// Initializes model info with the model identifier, vendor identifier, and supported protocol set.
    /// </summary>
    /// <param name="modelSlug">
    /// 模型唯一标识。
    /// Unique model identifier.
    /// </param>
    /// <param name="vendorSlug">
    /// 供应商标识。
    /// Vendor identifier.
    /// </param>
    /// <param name="modelProtocols">
    /// 模型支持的协议位标志。
    /// Bit flags describing the protocols supported by the model.
    /// </param>
    /// <param name="supportsStructuredOutputs">
    /// 是否显式声明支持结构化输出；未知时为 <c>null</c>。
    /// Whether the model explicitly advertises structured-output support; <c>null</c> when unknown.
    /// </param>
    public ModelInfo(
        string modelSlug,
        string vendorSlug,
        ModelProtocolType modelProtocols,
        bool? supportsStructuredOutputs = null)
    {
        ModelSlug = modelSlug;
        VendorSlug = vendorSlug;
        ModelProtocols = modelProtocols;
        SupportsStructuredOutputs = supportsStructuredOutputs;
    }

    /// <summary>
    /// 模型唯一标识。
    /// Unique model identifier.
    /// </summary>
    public string ModelSlug { get; init; }

    /// <summary>
    /// 供应商标识。
    /// Vendor identifier.
    /// </summary>
    public string VendorSlug { get; init; }

    /// <summary>
    /// 模型支持的协议集合。
    /// Supported protocol set for the model.
    /// </summary>
    public ModelProtocolType ModelProtocols { get; init; }

    /// <summary>
    /// 模型是否显式支持结构化输出。
    /// Whether the model explicitly supports structured outputs.
    /// </summary>
    public bool? SupportsStructuredOutputs { get; init; }
}

/// <summary>
/// 模型协议类型位标志。
/// Bit flags describing supported model protocols.
/// </summary>
public enum ModelProtocolType : short
{
    /// <summary>
    /// 未声明任何协议。
    /// No protocol is declared.
    /// </summary>
    None = 0,

    /// <summary>
    /// OpenAI Chat Completions 协议。
    /// OpenAI Chat Completions protocol.
    /// </summary>
    OpenAIChatCompletions = 1 << 0,

    /// <summary>
    /// OpenAI Responses 协议。
    /// OpenAI Responses protocol.
    /// </summary>
    OpenAIResponse = 1 << 1,

    /// <summary>
    /// Anthropic Messages 协议。
    /// Anthropic Messages protocol.
    /// </summary>
    AnthropicMessages = 1 << 2,

    /// <summary>
    /// Google Generate Content 协议。
    /// Google Generate Content protocol.
    /// </summary>
    GoogleGenerateContent = 1 << 3
}
