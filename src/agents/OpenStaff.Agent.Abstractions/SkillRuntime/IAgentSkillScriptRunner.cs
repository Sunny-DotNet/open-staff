using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OpenStaff.Agent;

#pragma warning disable MAAI001
/// <summary>
/// 为文件型 skill 脚本提供宿主执行入口。
/// </summary>
public interface IAgentSkillScriptRunner
{
    /// <summary>
    /// 当前宿主允许发现并执行的脚本扩展名。
    /// </summary>
    IReadOnlyList<string> AllowedScriptExtensions { get; }

    /// <summary>
    /// 执行一个已解析的文件型 skill 脚本。
    /// </summary>
    Task<object?> RunAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken);
}
#pragma warning restore MAAI001
