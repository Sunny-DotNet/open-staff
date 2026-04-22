using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Plugins;

namespace OpenStaff.Plugins;

/// <summary>
/// 负责从插件目录发现并初始化可扩展组件。
/// Discovers and initializes extensibility components from the plugin directory.
/// </summary>
public class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<IPlugin> _loadedPlugins = new();

    /// <summary>
    /// 初始化插件加载器。
    /// Initializes the plugin loader.
    /// </summary>
    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 当前已经成功初始化的插件集合。
    /// The plugins that have been successfully initialized.
    /// </summary>
    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins;

    /// <summary>
    /// 扫描并加载指定目录中的插件程序集。
    /// Scans and loads plugin assemblies from the specified directory.
    /// </summary>
    /// <param name="pluginsDirectory">插件目录。/ The plugin directory.</param>
    /// <param name="serviceProvider">传递给插件初始化阶段的服务提供程序。/ The service provider passed to plugin initialization.</param>
    public async Task LoadPluginsAsync(string pluginsDirectory, IServiceProvider serviceProvider)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            _logger.LogWarning("插件目录不存在: {Dir}", pluginsDirectory);
            return;
        }

        foreach (var dllFile in Directory.GetFiles(pluginsDirectory, "*.dll"))
        {
            try
            {
                // zh-CN: 通过反射扫描每个 DLL 中的 IPlugin 实现，避免要求插件额外维护中心注册表。
                // en: Scan each DLL for IPlugin implementations via reflection so plugins do not need to maintain a separate central registry.
                var assembly = Assembly.LoadFrom(dllFile);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                    await plugin.InitializeAsync(serviceProvider);
                    _loadedPlugins.Add(plugin);
                    _logger.LogInformation("插件已加载: {Name} v{Version}", plugin.Manifest.Name, plugin.Manifest.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载插件失败: {File}", dllFile);
            }
        }
    }

    /// <summary>
    /// 获取所有代理角色插件。
    /// Gets all agent-role plugins.
    /// </summary>
    /// <returns>已加载插件中的代理角色扩展。/ Agent-role extensions from the loaded plugins.</returns>
    public IEnumerable<IAgentPlugin> GetAgentPlugins()
    {
        return _loadedPlugins.OfType<IAgentPlugin>();
    }
}
