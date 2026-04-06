using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Vendor;

/// <summary>
/// Vendor 智能体供应商接口 — 每个大厂 SDK 实现此接口
/// </summary>
public interface IVendorAgentProvider
{
    /// <summary>供应商标识（如 "anthropic", "google", "github-copilot"）</summary>
    string VendorType { get; }

    /// <summary>显示名称（如 "Anthropic Claude"）</summary>
    string DisplayName { get; }

    /// <summary>获取该 Vendor 的配置 Schema（前端据此渲染动态表单）</summary>
    VendorConfigSchema GetConfigSchema();

    /// <summary>创建 Vendor 智能体实例</summary>
    IAgent CreateAgent(AgentRole role, VendorConfig config);
}
