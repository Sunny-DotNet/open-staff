namespace OpenStaff.Core.Modularity;

/// <summary>
/// 应用初始化上下文，在 OnApplicationInitialization 阶段传递给各模块。
/// </summary>
public class ApplicationInitializationContext
{
    public IServiceProvider ServiceProvider { get; }

    public ApplicationInitializationContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }
}
