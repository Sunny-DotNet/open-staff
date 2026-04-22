namespace OpenStaff.Core.Plugins;

/// <summary>
/// 插件清单 / Plugin manifest
/// </summary>
public class PluginManifest
{
    /// <summary>插件名称 / Plugin name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>插件版本 / Plugin version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>角色类型标识 / Optional role type identifier exposed by the plugin.</summary>
    public string? RoleType { get; set; }

    /// <summary>插件描述 / Plugin description.</summary>
    public string? Description { get; set; }

    /// <summary>系统提示词文本 / Default system prompt text.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>作者信息 / Author information.</summary>
    public string? Author { get; set; }

    /// <summary>扩展配置 / Extension configuration values.</summary>
    public Dictionary<string, object> Config { get; set; } = new();
}
