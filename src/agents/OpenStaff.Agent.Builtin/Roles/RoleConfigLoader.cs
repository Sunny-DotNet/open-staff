using System.Reflection;
using System.Text.Json;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agent.Builtin.Roles;

/// <summary>
/// 从嵌入资源加载角色配置
/// </summary>
public static class RoleConfigLoader
{
    /// <summary>加载内置角色配置（仅 secretary）</summary>
    public static IReadOnlyList<RoleConfig> LoadBuiltin()
    {
        var assembly = typeof(RoleConfigLoader).Assembly;
        var configs = new List<RoleConfig>();

        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.Contains(".Roles.secretary.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName != null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var config = JsonSerializer.Deserialize<RoleConfig>(stream);
                if (config != null)
                    configs.Add(config);
            }
        }

        return configs;
    }

    /// <summary>加载所有嵌入的角色配置（包括旧模板）</summary>
    public static IReadOnlyList<RoleConfig> LoadAll()
    {
        var assembly = typeof(RoleConfigLoader).Assembly;
        var configs = new List<RoleConfig>();

        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Roles.") && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            var config = JsonSerializer.Deserialize<RoleConfig>(stream);
            if (config != null)
                configs.Add(config);
        }

        return configs;
    }
}
