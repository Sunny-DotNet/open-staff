using Microsoft.Agents.AI;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Agent;

/// <summary>
/// 统一的智能体供应商接口 — 所有 Agent 项目（Builtin / Vendor）实现此接口
/// </summary>
public interface IAgentProvider
{
    /// <summary>供应商标识（如 "builtin", "anthropic", "google", "github-copilot"）</summary>
    string ProviderType { get; }

    /// <summary>显示名称（如 "内置标准", "Anthropic Claude"）</summary>
    string DisplayName { get; }

    /// <summary>供应商品牌头像 data URI（Vendor 返回 SVG，Builtin 返回 null）</summary>
    string? AvatarDataUri => null;

    /// <summary>获取该供应商的配置 Schema（前端据此渲染动态表单）</summary>
    AgentConfigSchema GetConfigSchema();

    /// <summary>根据数据库角色配置创建 AIAgent 实例</summary>
    Task<AIAgent> CreateAgentAsync(AgentRole role, AgentContext context, ResolvedProvider provider);
}
