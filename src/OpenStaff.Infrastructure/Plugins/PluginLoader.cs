using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Plugins;

namespace OpenStaff.Infrastructure.Plugins;

/// <summary>
/// 插件加载器 / Plugin loader
/// </summary>
public class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<IPlugin> _loadedPlugins = new();

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins;

    /// <summary>
    /// 扫描并加载插件目录 / Scan and load plugins from directory
    /// </summary>
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
    /// 获取所有角色插件 / Get all agent plugins
    /// </summary>
    public IEnumerable<IAgentPlugin> GetAgentPlugins()
    {
        return _loadedPlugins.OfType<IAgentPlugin>();
    }
}
