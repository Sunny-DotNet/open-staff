namespace OpenStaff.Core.Models;

/// <summary>
/// 智能体来源类型
/// </summary>
public enum AgentSource
{
    /// <summary>自定义智能体（用户创建）</summary>
    Custom = 0,

    /// <summary>内置智能体（秘书）</summary>
    Builtin = 1,

    /// <summary>远程智能体（从 OpenHire 平台导入，预留）</summary>
    Remote = 2,

    /// <summary>大厂供应商智能体（Anthropic / Google / GitHub Copilot 等）</summary>
    Vendor = 3
}
