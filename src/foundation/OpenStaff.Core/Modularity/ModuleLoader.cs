using System.Reflection;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// 模块加载器 / Module loader that discovers dependencies, topologically sorts them, and creates module instances.
/// </summary>
public static class ModuleLoader
{
    /// <summary>
    /// 从启动模块开始，递归收集所有依赖模块并拓扑排序 / Collect all modules reachable from the startup module and return them in dependency order.
    /// </summary>
    /// <returns>
    /// 按依赖顺序排列并已实例化的模块列表；每个模块类型都必须可通过无参构造函数创建 / Instantiated modules ordered by dependency; each module type must support parameterless construction.
    /// </returns>
    public static IReadOnlyList<OpenStaffModule> LoadModules<TStartupModule>(IEnumerable<Type>? additionalRootModules = null)
        where TStartupModule : OpenStaffModule
    {
        var moduleTypes = new List<Type>();
        var visited = new HashSet<Type>();

        CollectModuleTypes(typeof(TStartupModule), moduleTypes, visited);
        if (additionalRootModules != null)
        {
            foreach (var additionalRootModule in additionalRootModules)
                CollectModuleTypes(additionalRootModule, moduleTypes, visited);
        }

        return TopologicalSort(moduleTypes)
            .Select(t => (OpenStaffModule)Activator.CreateInstance(t)!)
            .ToList();
    }

    /// <summary>
    /// 递归收集模块类型并去重 / Recursively collect module types while preventing duplicate traversal.
    /// </summary>
    /// <param name="moduleType">当前待检查的模块类型 / Module type currently being inspected.</param>
    /// <param name="result">收集到的模块类型列表 / Accumulated module type list.</param>
    /// <param name="visited">已访问模块类型集合 / Set of module types that were already visited.</param>
    /// <exception cref="InvalidOperationException">当类型未继承 <see cref="OpenStaffModule"/> 时抛出 / Thrown when the type does not inherit from <see cref="OpenStaffModule"/>.</exception>
    private static void CollectModuleTypes(Type moduleType, List<Type> result, HashSet<Type> visited)
    {
        if (!visited.Add(moduleType))
            return;

        if (!typeof(OpenStaffModule).IsAssignableFrom(moduleType))
            throw new InvalidOperationException(
                $"Type '{moduleType.FullName}' is not an OpenStaffModule.");

        var dependsOn = moduleType.GetCustomAttribute<DependsOnAttribute>();
        if (dependsOn != null)
        {
            foreach (var dep in dependsOn.DependedModuleTypes)
            {
                // zh-CN: 先递归收集依赖，再把当前模块加入结果，便于后续拓扑排序保持依赖优先。
                // en: Collect dependencies first, then add the current module so the later topological sort preserves dependency-first ordering.
                CollectModuleTypes(dep, result, visited);
            }
        }

        result.Add(moduleType);
    }

    /// <summary>
    /// 使用 Kahn 算法进行拓扑排序，确保依赖模块先于依赖它们的模块 / Topologically sort module types with Kahn's algorithm so prerequisites appear before dependants.
    /// </summary>
    /// <param name="moduleTypes">待排序的模块类型列表 / Module types to sort.</param>
    /// <returns>排序后的模块类型列表 / Sorted module types.</returns>
    private static List<Type> TopologicalSort(List<Type> moduleTypes)
    {
        var graph = new Dictionary<Type, List<Type>>();
        var inDegree = new Dictionary<Type, int>();

        foreach (var t in moduleTypes)
        {
            graph[t] = [];
            inDegree.TryAdd(t, 0);
        }

        foreach (var t in moduleTypes)
        {
            var deps = t.GetCustomAttribute<DependsOnAttribute>()?.DependedModuleTypes ?? [];
            foreach (var dep in deps)
            {
                if (graph.ContainsKey(dep))
                {
                    // zh-CN: 建立 “依赖项 -> 被依赖者” 的边，让入度表示尚未满足的依赖数量。
                    // en: Build edges from dependency to dependant so indegree reflects the number of unsatisfied prerequisites.
                    graph[dep].Add(t);
                    inDegree[t] = inDegree.GetValueOrDefault(t) + 1;
                }
            }
        }

        // zh-CN: 只有没有未满足依赖的模块才能首先初始化。
        // en: Only modules without outstanding dependencies can be initialized first.
        var queue = new Queue<Type>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<Type>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            foreach (var neighbor in graph[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        // zh-CN: 若未能输出全部节点，则说明图中仍存在环形依赖。
        // en: If some nodes remain unsorted, the dependency graph still contains a cycle.
        if (sorted.Count != moduleTypes.Count)
            throw new InvalidOperationException("Circular dependency detected among OpenStaff modules.");

        return sorted;
    }
}
