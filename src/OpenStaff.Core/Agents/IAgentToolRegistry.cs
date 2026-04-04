namespace OpenStaff.Core.Agents;

/// <summary>
/// 工具注册表接口 / Tool registry interface
/// </summary>
public interface IAgentToolRegistry
{
    void Register(IAgentTool tool);
    IAgentTool? GetTool(string name);
    IReadOnlyList<IAgentTool> GetTools(IEnumerable<string> names);
    IReadOnlyList<IAgentTool> GetAllTools();
}
