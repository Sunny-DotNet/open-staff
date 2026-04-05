using System.Reflection;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// 模块加载器：扫描 [DependsOn] 依赖，拓扑排序，实例化模块。
/// </summary>
public static class ModuleLoader
{
    /// <summary>
    /// 从启动模块开始，递归收集所有依赖模块并拓扑排序。
    /// </summary>
    public static IReadOnlyList<OpenStaffModule> LoadModules<TStartupModule>()
        where TStartupModule : OpenStaffModule
    {
        var moduleTypes = new List<Type>();
        var visited = new HashSet<Type>();

        CollectModuleTypes(typeof(TStartupModule), moduleTypes, visited);

        return TopologicalSort(moduleTypes)
            .Select(t => (OpenStaffModule)Activator.CreateInstance(t)!)
            .ToList();
    }

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
                CollectModuleTypes(dep, result, visited);
            }
        }

        result.Add(moduleType);
    }

    /// <summary>
    /// Kahn 算法拓扑排序，确保依赖模块先于被依赖模块加载。
    /// </summary>
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
                    graph[dep].Add(t);
                    inDegree[t] = inDegree.GetValueOrDefault(t) + 1;
                }
            }
        }

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

        if (sorted.Count != moduleTypes.Count)
            throw new InvalidOperationException("Circular dependency detected among OpenStaff modules.");

        return sorted;
    }
}
