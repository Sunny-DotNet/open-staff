namespace OpenStaff.Core.Orchestration;

/// <summary>
/// 任务依赖图 / Task dependency graph
/// </summary>
public class TaskGraph
{
    private readonly Dictionary<Guid, TaskNode> _nodes = new();

    /// <summary>
    /// 添加或替换任务节点 / Add or replace a task node in the in-memory scheduling graph.
    /// </summary>
    /// <param name="taskId">任务标识 / Task identifier.</param>
    /// <param name="title">任务标题 / Task title.</param>
    /// <param name="priority">任务优先级 / Task priority.</param>
    /// <remarks>
    /// 重复传入同一个 <paramref name="taskId"/> 会覆盖现有节点，并清空此前记录在旧节点上的依赖，因此通常应先注册任务再追加依赖。
    /// Reusing the same <paramref name="taskId"/> replaces the existing node and drops any dependencies stored on the old instance, so tasks should normally be added before dependencies are recorded.
    /// </remarks>
    public void AddTask(Guid taskId, string title, int priority = 0)
    {
        _nodes[taskId] = new TaskNode { TaskId = taskId, Title = title, Priority = priority };
    }

    /// <summary>
    /// 为已注册任务添加前置依赖 / Add a prerequisite edge for a previously registered task.
    /// </summary>
    /// <param name="taskId">依赖方任务标识 / Dependent task identifier.</param>
    /// <param name="dependsOnId">前置任务标识 / Prerequisite task identifier.</param>
    /// <remarks>
    /// 如果 <paramref name="taskId"/> 尚未注册，方法会静默忽略该调用；调用方需要自行保证节点先于依赖边创建。
    /// The call is ignored when <paramref name="taskId"/> has not been registered yet, so callers must ensure nodes are created before edges are added.
    /// </remarks>
    public void AddDependency(Guid taskId, Guid dependsOnId)
    {
        if (_nodes.TryGetValue(taskId, out var node))
        {
            node.Dependencies.Add(dependsOnId);
        }
    }

    /// <summary>
    /// 获取当前可执行的任务 / Get the tasks that are currently ready to execute.
    /// </summary>
    /// <param name="completedTasks">已完成任务集合 / Set of completed task identifiers.</param>
    /// <returns>
    /// 尚未完成且其所有依赖都已满足的任务，按优先级从高到低排序 / Incomplete tasks whose dependencies are all satisfied, ordered by descending priority.
    /// </returns>
    public IReadOnlyList<TaskNode> GetReadyTasks(ISet<Guid> completedTasks)
    {
        return _nodes.Values
            .Where(n => !completedTasks.Contains(n.TaskId) &&
                        n.Dependencies.All(d => completedTasks.Contains(d)))
            .OrderByDescending(n => n.Priority)
            .ToList();
    }

    /// <summary>
    /// 检测图中是否存在循环依赖 / Detect whether the registered graph contains a dependency cycle.
    /// </summary>
    /// <returns>存在环时返回 <c>true</c> / <c>true</c> when a cycle is found.</returns>
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

    /// <summary>
    /// 深度优先遍历单个节点的依赖链以检测回边 / Walk one dependency chain with depth-first search to detect back edges.
    /// </summary>
    /// <param name="nodeId">当前检查的节点 / Node currently being inspected.</param>
    /// <param name="visited">已完成展开的节点集合 / Nodes that were fully explored already.</param>
    /// <param name="inStack">当前递归路径上的节点集合 / Nodes on the active recursion stack.</param>
    /// <returns>发现回边时返回 <c>true</c> / <c>true</c> when a back edge is encountered.</returns>
    private bool HasCycleDfs(Guid nodeId, HashSet<Guid> visited, HashSet<Guid> inStack)
    {
        // zh-CN: inStack 表示当前 DFS 路径；再次遇到同一节点说明存在回边，也就存在环。
        // en: inStack tracks the current DFS path; seeing the same node again indicates a back edge and therefore a cycle.
        if (inStack.Contains(nodeId)) return true;

        // zh-CN: 已完全展开过的节点无需重复遍历。
        // en: Nodes that were fully explored earlier can be skipped.
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

/// <summary>
/// 任务图节点 / Node within a <see cref="TaskGraph"/>.
/// </summary>
public class TaskNode
{
    /// <summary>任务标识 / Task identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>任务标题 / Task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>任务优先级 / Task priority.</summary>
    public int Priority { get; set; }

    /// <summary>依赖任务标识集合 / Identifiers of prerequisite tasks.</summary>
    public List<Guid> Dependencies { get; set; } = new();
}
