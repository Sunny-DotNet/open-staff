namespace OpenStaff.Core.Agents;

/// <summary>
/// 会话场景类型 / Conversation scene types.
/// </summary>
public enum SceneType
{
    /// <summary>测试或诊断场景 / Test or diagnostic scenario.</summary>
    Test,

    /// <summary>团队级群聊场景 / Team-wide group chat scenario.</summary>
    TeamGroup,

    /// <summary>项目头脑风暴场景 / Project brainstorming scenario.</summary>
    ProjectBrainstorm,

    /// <summary>项目执行群聊场景 / Project execution group scenario.</summary>
    ProjectGroup,

    /// <summary>私聊场景 / Private one-to-one scenario.</summary>
    Private
}
