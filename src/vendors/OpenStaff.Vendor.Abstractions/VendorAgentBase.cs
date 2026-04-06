using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;

namespace OpenStaff.Vendor;

/// <summary>
/// Vendor 智能体基类 — 提供通用初始化和状态管理
/// </summary>
public abstract class VendorAgentBase : AgentBase
{
    protected VendorAgentBase(ILogger logger) : base(logger) { }
}
