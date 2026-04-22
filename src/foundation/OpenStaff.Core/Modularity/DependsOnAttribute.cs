namespace OpenStaff.Core.Modularity;

/// <summary>
/// 声明当前模块依赖的其他模块类型，用于拓扑排序加载 / Declares the module types required by the current module so they can be loaded in dependency order.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DependsOnAttribute : Attribute
{
    /// <summary>依赖模块类型列表 / Dependent module types.</summary>
    public Type[] DependedModuleTypes { get; }

    /// <summary>创建依赖声明属性 / Create the dependency declaration.</summary>
    /// <param name="dependedModuleTypes">依赖模块类型 / Module types the current module depends on.</param>
    public DependsOnAttribute(params Type[] dependedModuleTypes)
    {
        DependedModuleTypes = dependedModuleTypes;
    }
}
