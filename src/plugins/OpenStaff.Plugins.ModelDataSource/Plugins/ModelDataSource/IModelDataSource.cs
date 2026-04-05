using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Plugins.ModelDataSource;

public interface IModelDataSource
{
    Task<IReadOnlyCollection<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default);
}
public record struct ModelData(
    string Id,
    string Name,
    string Vendor,
    string Family,
    DateTime ReleaseAt,
    ModelModality Modalities);
public record struct ModelCost(
    decimal? Input,
    decimal? Output,
    decimal? Reasoning,
    decimal? CacheRead,
    decimal? CacheWrite
);
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
    Embeddings = 1 << 14, // 特例：向量嵌入（输入/输出均算），不与常规模态冲突，单独一位
    Other = 1 << 15
}


/// <summary>模型能力特性（位标志）</summary>
[Flags]
public enum ModelCapability
{
    None = 0,
    Streaming = 1 << 0,
    FunctionCall = 1 << 1,
    JsonMode = 1 << 2,
    Vision = 1 << 3,  // 图像理解（输入图片）
    Reasoning = 1 << 4,  // 链式推理（o1/o3/thinking 系列）
    Embedding = 1 << 5,  // 向量嵌入
}
