namespace OpenStaff.Plugins.ModelDataSource;

/// <summary>
/// 模型数据源接口 — 从不同来源（models.dev、供应商 API 等）获取模型元数据
/// </summary>
public interface IModelDataSource
{
    /// <summary>数据源唯一标识（如 "models.dev"）</summary>
    string SourceId { get; }

    /// <summary>数据源名称</summary>
    string DisplayName { get; }

    /// <summary>数据是否已加载就绪</summary>
    bool IsReady { get; }

    /// <summary>初始化数据源（下载/缓存等）</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>刷新数据</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>获取所有供应商信息</summary>
    Task<IReadOnlyList<ModelVendor>> GetVendorsAsync(CancellationToken cancellationToken = default);

    /// <summary>获取所有模型</summary>
    Task<IReadOnlyList<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>获取指定供应商的模型</summary>
    Task<IReadOnlyList<ModelData>> GetModelsByVendorAsync(string vendorId, CancellationToken cancellationToken = default);

    /// <summary>获取单个模型（供应商 + 模型 ID 唯一标识）</summary>
    Task<ModelData?> GetModelAsync(string vendorId, string modelId, CancellationToken cancellationToken = default);

    /// <summary>最后更新时间</summary>
    DateTime? LastUpdatedUtc { get; }
}

/// <summary>模型供应商信息</summary>
public record ModelVendor(
    string Id,
    string Name,
    string? ApiBaseUrl,
    string? DocumentationUrl,
    IReadOnlyList<string> EnvVarNames);

/// <summary>模型数据</summary>
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

/// <summary>模型上下文窗口与输出限制</summary>
public record ModelLimits(
    long? ContextWindow,
    long? MaxInput,
    long? MaxOutput);

/// <summary>模型价格（美元/百万 token）</summary>
public record ModelPricing(
    string? Input,
    string? Output,
    string? CacheRead,
    string? CacheWrite);

/// <summary>模型支持的输入/输出模态（位标志）</summary>
[Flags]
public enum ModelModality : ushort
{
    None = 0,
    Text = 1 << 0,
    Image = 1 << 1,
    Audio = 1 << 2,
    Video = 1 << 3,
    File = 1 << 4,
    Embeddings = 1 << 14,
    Other = 1 << 15
}

/// <summary>模型能力特性（位标志）</summary>
[Flags]
public enum ModelCapability : uint
{
    None = 0,
    Streaming = 1 << 0,
    FunctionCall = 1 << 1,
    JsonMode = 1 << 2,
    Vision = 1 << 3,
    Reasoning = 1 << 4,
    Embedding = 1 << 5,
    Attachment = 1 << 6,
    OpenWeights = 1 << 7,
}
