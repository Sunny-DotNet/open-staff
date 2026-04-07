using OpenStaff.Core.Agents;

namespace OpenStaff.Agent;

/// <summary>
/// Vendor（厂商）智能体供应商 — 继承 IAgentProvider，额外提供动态模型列表查询
/// </summary>
public interface IVendorAgentProvider : IAgentProvider
{
    /// <summary>
    /// 获取该供应商支持的模型列表。
    /// 默认实现可从 IModelDataSource 查询，各厂商可 override 接入自家 API。
    /// </summary>
    Task<IReadOnlyList<VendorModel>> GetModelsAsync(CancellationToken ct = default);
}

/// <summary>
/// 供应商模型信息
/// </summary>
public record VendorModel(
    string Id,
    string Name,
    string? Family = null,
    string? Description = null);
