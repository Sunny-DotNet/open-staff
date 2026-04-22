namespace OpenStaff.Core.Plugins;

/// <summary>
/// 插件接口 / Plugin interface
/// </summary>
public interface IPlugin
{
    /// <summary>插件清单 / Plugin manifest.</summary>
    PluginManifest Manifest { get; }

    /// <summary>初始化插件 / Initialize the plugin.</summary>
    /// <param name="serviceProvider">应用服务提供器 / Application service provider.</param>
    Task InitializeAsync(IServiceProvider serviceProvider);

    /// <summary>关闭插件 / Shut down the plugin.</summary>
    Task ShutdownAsync();
}
