namespace OpenStaff.Agent;

/// <summary>
/// Agent 运行时配置 — 从数据库 Config JSON 列解析出的键值对
/// </summary>
public class AgentConfig
{
    /// <summary>配置键值对（key 对应 AgentConfigField.Key）</summary>
    public Dictionary<string, string?> Values { get; set; } = [];

    /// <summary>获取配置值</summary>
    public string? Get(string key) =>
        Values.TryGetValue(key, out var value) ? value : null;

    /// <summary>获取必填配置值（不存在则抛异常）</summary>
    public string GetRequired(string key) =>
        Get(key) ?? throw new InvalidOperationException($"Agent config '{key}' is required but not set");

    /// <summary>从 JSON 字符串解析配置</summary>
    public static AgentConfig FromJson(string? json)
    {
        var config = new AgentConfig();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
                if (values != null)
                    config.Values = values;
            }
            catch { /* ignore parse errors */ }
        }
        return config;
    }
}
