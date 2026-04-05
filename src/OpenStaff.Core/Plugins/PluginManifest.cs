namespace OpenStaff.Core.Plugins;

/// <summary>
/// 插件清单 / Plugin manifest
/// </summary>
public class PluginManifest
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? RoleType { get; set; } // 角色类型标识
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Author { get; set; }
    public Dictionary<string, object> Config { get; set; } = new();
}
