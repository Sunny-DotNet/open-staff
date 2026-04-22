namespace OpenStaff.Plugins.ModelDataSource;

/// <summary>
/// 模型数据源接口，为 Provider 与其他扩展点提供统一的模型元数据读取能力。
/// Model data source contract that offers a uniform way for providers and other extension points to read model metadata.
/// </summary>
public interface IModelDataSource
{
    /// <summary>
    /// 数据源唯一标识。
    /// Unique data source identifier.
    /// </summary>
    string SourceId { get; }

    /// <summary>
    /// 数据源显示名称。
    /// Human-readable data source name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 指示数据源是否已完成初始化。
    /// Indicates whether the data source has completed initialization.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// 数据源最近更新时间（UTC）。
    /// Last update time of the data source in UTC.
    /// </summary>
    DateTime? LastUpdatedUtc { get; }

    /// <summary>
    /// 初始化数据源，例如加载缓存或首次下载数据。
    /// Initializes the data source, such as loading a cache or downloading data for the first time.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新数据源内容。
    /// Refreshes the data source content.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有供应商信息。
    /// Gets metadata for all vendors.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 供应商集合。
    /// Collection of vendors.
    /// </returns>
    Task<IReadOnlyList<ModelVendor>> GetVendorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有模型信息。
    /// Gets metadata for all models.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 模型集合。
    /// Collection of models.
    /// </returns>
    Task<IReadOnlyList<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定供应商的模型列表。
    /// Gets the models associated with a specific vendor.
    /// </summary>
    /// <param name="vendorId">
    /// 供应商标识。
    /// Vendor identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 指定供应商的模型集合。
    /// Collection of models for the specified vendor.
    /// </returns>
    Task<IReadOnlyList<ModelData>> GetModelsByVendorAsync(string vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定供应商下的单个模型。
    /// Gets a single model for the specified vendor.
    /// </summary>
    /// <param name="vendorId">
    /// 供应商标识。
    /// Vendor identifier.
    /// </param>
    /// <param name="modelId">
    /// 模型标识。
    /// Model identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 模型信息；未找到时返回 <see langword="null" />。
    /// Model metadata, or <see langword="null" /> when no matching model exists.
    /// </returns>
    Task<ModelData?> GetModelAsync(string vendorId, string modelId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 模型供应商信息。
/// Model vendor information.
/// </summary>
/// <param name="Id">
/// 供应商标识。
/// Vendor identifier.
/// </param>
/// <param name="Name">
/// 供应商名称。
/// Vendor name.
/// </param>
/// <param name="ApiBaseUrl">
/// 供应商 API 基础地址。
/// Vendor API base URL.
/// </param>
/// <param name="DocumentationUrl">
/// 供应商文档地址。
/// Vendor documentation URL.
/// </param>
/// <param name="EnvVarNames">
/// 相关环境变量名称列表。
/// Related environment variable names.
/// </param>
public record ModelVendor(
    string Id,
    string Name,
    string? ApiBaseUrl,
    string? DocumentationUrl,
    IReadOnlyList<string> EnvVarNames);

/// <summary>
/// 模型元数据。
/// Model metadata.
/// </summary>
/// <param name="Id">
/// 模型标识。
/// Model identifier.
/// </param>
/// <param name="Name">
/// 模型名称。
/// Model name.
/// </param>
/// <param name="VendorId">
/// 供应商标识。
/// Vendor identifier.
/// </param>
/// <param name="Family">
/// 模型家族。
/// Model family.
/// </param>
/// <param name="ReleasedAt">
/// 发布时间。
/// Release time.
/// </param>
/// <param name="InputModalities">
/// 输入模态。
/// Input modalities.
/// </param>
/// <param name="OutputModalities">
/// 输出模态。
/// Output modalities.
/// </param>
/// <param name="Capabilities">
/// 能力标志。
/// Capability flags.
/// </param>
/// <param name="Limits">
/// 限制信息。
/// Limit information.
/// </param>
/// <param name="Pricing">
/// 价格信息。
/// Pricing information.
/// </param>
public record ModelData(
    string Id,
    string Name,
    string VendorId,
    string? Family,
    DateTime? ReleasedAt,
    ModelModality InputModalities,
    ModelModality OutputModalities,
    ModelCapability Capabilities,
    ModelLimits Limits,
    ModelPricing Pricing);

/// <summary>
/// 模型上下文窗口与输出限制。
/// Model context window and output limits.
/// </summary>
/// <param name="ContextWindow">
/// 上下文窗口大小。
/// Context window size.
/// </param>
/// <param name="MaxInput">
/// 最大输入长度。
/// Maximum input length.
/// </param>
/// <param name="MaxOutput">
/// 最大输出长度。
/// Maximum output length.
/// </param>
public record ModelLimits(
    long? ContextWindow,
    long? MaxInput,
    long? MaxOutput);

/// <summary>
/// 模型价格信息（美元/百万 token）。
/// Model pricing information in USD per million tokens.
/// </summary>
/// <param name="Input">
/// 输入价格。
/// Input price.
/// </param>
/// <param name="Output">
/// 输出价格。
/// Output price.
/// </param>
/// <param name="CacheRead">
/// 缓存读取价格。
/// Cache read price.
/// </param>
/// <param name="CacheWrite">
/// 缓存写入价格。
/// Cache write price.
/// </param>
public record ModelPricing(
    string? Input,
    string? Output,
    string? CacheRead,
    string? CacheWrite);

/// <summary>
/// 模型支持的输入输出模态位标志。
/// Bit flags describing supported input and output modalities.
/// </summary>
[Flags]
public enum ModelModality : ushort
{
    /// <summary>
    /// 无模态。
    /// No modality.
    /// </summary>
    None = 0,

    /// <summary>
    /// 文本模态。
    /// Text modality.
    /// </summary>
    Text = 1 << 0,

    /// <summary>
    /// 图像模态。
    /// Image modality.
    /// </summary>
    Image = 1 << 1,

    /// <summary>
    /// 音频模态。
    /// Audio modality.
    /// </summary>
    Audio = 1 << 2,

    /// <summary>
    /// 视频模态。
    /// Video modality.
    /// </summary>
    Video = 1 << 3,

    /// <summary>
    /// 文件模态。
    /// File modality.
    /// </summary>
    File = 1 << 4,

    /// <summary>
    /// 向量嵌入模态。
    /// Embeddings modality.
    /// </summary>
    Embeddings = 1 << 14,

    /// <summary>
    /// 其他未分类模态。
    /// Other uncategorized modalities.
    /// </summary>
    Other = 1 << 15
}

/// <summary>
/// 模型能力位标志。
/// Bit flags describing model capabilities.
/// </summary>
[Flags]
public enum ModelCapability : uint
{
    /// <summary>
    /// 无能力标志。
    /// No capability flag.
    /// </summary>
    None = 0,

    /// <summary>
    /// 支持流式输出。
    /// Supports streaming output.
    /// </summary>
    Streaming = 1 << 0,

    /// <summary>
    /// 支持函数或工具调用。
    /// Supports function or tool calling.
    /// </summary>
    FunctionCall = 1 << 1,

    /// <summary>
    /// 支持结构化 JSON 输出。
    /// Supports structured JSON output.
    /// </summary>
    JsonMode = 1 << 2,

    /// <summary>
    /// 支持视觉输入。
    /// Supports vision input.
    /// </summary>
    Vision = 1 << 3,

    /// <summary>
    /// 支持推理能力。
    /// Supports reasoning capabilities.
    /// </summary>
    Reasoning = 1 << 4,

    /// <summary>
    /// 支持嵌入生成。
    /// Supports embedding generation.
    /// </summary>
    Embedding = 1 << 5,

    /// <summary>
    /// 支持附件处理。
    /// Supports attachment handling.
    /// </summary>
    Attachment = 1 << 6,

    /// <summary>
    /// 支持开放权重。
    /// Supports open weights.
    /// </summary>
    OpenWeights = 1 << 7,
}
