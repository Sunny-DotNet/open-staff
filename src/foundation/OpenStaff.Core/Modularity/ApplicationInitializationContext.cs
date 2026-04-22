namespace OpenStaff.Core.Modularity;

/// <summary>
/// 应用初始化上下文 / Application initialization context passed to modules during startup.
/// </summary>
public class ApplicationInitializationContext
{
    /// <summary>应用服务提供器 / Application service provider.</summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>创建初始化上下文 / Create the initialization context.</summary>
    /// <param name="serviceProvider">应用服务提供器 / Application service provider.</param>
    public ApplicationInitializationContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }
}
