using System.Reflection;
using System.Text.Json;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agents.Roles;

/// <summary>
/// 从嵌入资源加载角色配置 / Load role configs from embedded resources
/// </summary>
public static class RoleConfigLoader
{
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
