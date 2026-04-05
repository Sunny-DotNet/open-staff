namespace OpenStaff.Core.Modularity;

/// <summary>
/// 声明当前模块依赖的其他模块类型，用于拓扑排序加载。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DependsOnAttribute : Attribute
{
    public Type[] DependedModuleTypes { get; }

    public DependsOnAttribute(params Type[] dependedModuleTypes)
    {
        DependedModuleTypes = dependedModuleTypes;
    }
}
