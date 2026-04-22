namespace OpenStaff.Entities;

/// <summary>
/// 智能体来源类型 / Source categories for agent definitions.
/// </summary>
public enum AgentSource
{
    /// <summary>自定义智能体（用户创建） / User-created custom agent.</summary>
    Custom = 0,

    /// <summary>内置智能体（秘书） / Built-in agent, such as the secretary role.</summary>
    Builtin = 1,

    /// <summary>远程智能体（从 OpenHire 平台导入，预留） / Remote agent imported from an external platform.</summary>
    Remote = 2,

    /// <summary>大厂供应商智能体（Anthropic / Google / GitHub Copilot 等） / Vendor-provided agent from services such as Anthropic, Google, or GitHub Copilot.</summary>
    Vendor = 3
}
