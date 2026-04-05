namespace OpenStaff.Core.Plugins;

/// <summary>
/// 插件接口 / Plugin interface
/// </summary>
public interface IPlugin
{
    PluginManifest Manifest { get; }
    Task InitializeAsync(IServiceProvider serviceProvider);
    Task ShutdownAsync();
}
