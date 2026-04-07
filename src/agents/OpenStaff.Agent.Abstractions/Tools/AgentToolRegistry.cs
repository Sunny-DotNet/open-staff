using OpenStaff.Core.Agents;

namespace OpenStaff.Agent.Tools;

public class AgentToolRegistry : IAgentToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IAgentTool tool)
    {
        _tools[tool.Name] = tool;
    }

    public IAgentTool? GetTool(string name)
    {
        return _tools.GetValueOrDefault(name);
    }

    public IReadOnlyList<IAgentTool> GetTools(IEnumerable<string> names)
    {
        return names
            .Select(n => _tools.GetValueOrDefault(n))
            .Where(t => t != null)
            .Cast<IAgentTool>()
            .ToList();
    }

    public IReadOnlyList<IAgentTool> GetAllTools()
    {
        return _tools.Values.ToList();
    }
}
