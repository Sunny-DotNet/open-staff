using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Dtos;

/// <summary>
/// 模型数据同步状态。
/// Synchronization status for cached model data.
/// </summary>
public class ModelDataStatusDto
{
    /// <summary>缓存是否已就绪。 / Whether the cache is ready for use.</summary>
    public bool IsReady { get; set; }

    /// <summary>最近更新时间（UTC）。 / Last update time in UTC.</summary>
    public DateTime? LastUpdatedUtc { get; set; }

    /// <summary>当前使用的数据源标识。 / Identifier of the active data source.</summary>
    public string? SourceId { get; set; }

    /// <summary>已同步的厂商数量。 / Number of synchronized vendors.</summary>
    public int VendorCount { get; set; }
}

/// <summary>
/// 模型目录中的单个模型条目。
/// Single model entry returned by the model catalog.
/// </summary>
public class ModelDataDto
{
    /// <summary>模型标识。 / Model identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>模型名称。 / Model name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>模型描述。 / Model description.</summary>
    public string? Description { get; set; }

    /// <summary>是否支持推理类回答。 / Whether the model supports reasoning-oriented responses.</summary>
    public bool Reasoning { get; set; }

    /// <summary>是否支持工具调用。 / Whether the model supports tool calling.</summary>
    public bool ToolCall { get; set; }

    /// <summary>是否支持附件输入。 / Whether the model supports attachments.</summary>
    public bool Attachment { get; set; }

    /// <summary>上下文窗口大小。 / Context window size.</summary>
    public long? ContextWindow { get; set; }

    /// <summary>最大输出 Token 数。 / Maximum output token count.</summary>
    public long? MaxOutput { get; set; }

    /// <summary>输入价格。 / Input price.</summary>
    public decimal? InputPrice { get; set; }

    /// <summary>输出价格。 / Output price.</summary>
    public decimal? OutputPrice { get; set; }

    /// <summary>支持的输入模态。 / Supported input modalities.</summary>
    public string? InputModalities { get; set; }

    /// <summary>支持的输出模态。 / Supported output modalities.</summary>
    public string? OutputModalities { get; set; }
}

/// <summary>
/// 模型提供商摘要。
/// Summary information for a model provider.
/// </summary>
public class ModelDataProviderDto
{
    /// <summary>提供商键。 / Provider key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>提供商名称。 / Provider name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>模型数量。 / Number of models available from the provider.</summary>
    public int ModelCount { get; set; }
}
