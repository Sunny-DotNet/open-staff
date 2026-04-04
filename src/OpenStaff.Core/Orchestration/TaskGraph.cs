namespace OpenStaff.Core.Orchestration;

/// <summary>
/// 任务依赖图 / Task dependency graph
/// </summary>
public class TaskGraph
{
    private readonly Dictionary<Guid, TaskNode> _nodes = new();

    public void AddTask(Guid taskId, string title, int priority = 0)
    {
        _nodes[taskId] = new TaskNode { TaskId = taskId, Title = title, Priority = priority };
    }

    public void AddDependency(Guid taskId, Guid dependsOnId)
    {
        if (_nodes.TryGetValue(taskId, out var node))
        {
            node.Dependencies.Add(dependsOnId);
        }
    }

    /// <summary>
    /// 获取可执行的任务(无未完成依赖) / Get tasks ready to execute
    /// </summary>
    public IReadOnlyList<TaskNode> GetReadyTasks(ISet<Guid> completedTasks)
    {
        return _nodes.Values
            .Where(n => !completedTasks.Contains(n.TaskId) &&
                        n.Dependencies.All(d => completedTasks.Contains(d)))
            .OrderByDescending(n => n.Priority)
            .ToList();
    }

    /// <summary>
    /// 检测循环依赖 / Detect circular dependencies
    /// </summary>
    public bool HasCycle()
    {
        var visited = new HashSet<Guid>();
        var inStack = new HashSet<Guid>();

        foreach (var nodeId in _nodes.Keys)
        {
            if (HasCycleDfs(nodeId, visited, inStack))
                return true;
        }
        return false;
    }

    private bool HasCycleDfs(Guid nodeId, HashSet<Guid> visited, HashSet<Guid> inStack)
    {
        if (inStack.Contains(nodeId)) return true;
        if (visited.Contains(nodeId)) return false;

        visited.Add(nodeId);
        inStack.Add(nodeId);

        if (_nodes.TryGetValue(nodeId, out var node))
        {
            foreach (var dep in node.Dependencies)
            {
                if (HasCycleDfs(dep, visited, inStack))
                    return true;
            }
        }

        inStack.Remove(nodeId);
        return false;
    }
}

public class TaskNode
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Priority { get; set; }
    public List<Guid> Dependencies { get; set; } = new();
}
