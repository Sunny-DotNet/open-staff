namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 表示从角色配置 JSON 解析出的运行时键值对。
/// en: Represents runtime key-value settings parsed from role configuration JSON.
/// </summary>
public class AgentConfig
{
    /// <summary>
    /// zh-CN: 获取或设置配置键值对，键名与角色配置 JSON 中持久化的原始字段保持一致。
    /// en: Gets or sets the configuration values using the raw keys persisted in the role-configuration JSON payload.
    /// </summary>
    public Dictionary<string, string?> Values { get; set; } = [];

    /// <summary>
    /// zh-CN: 按键读取可选配置值。
    /// en: Reads an optional configuration value by key.
    /// </summary>
    public string? Get(string key) =>
        Values.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// zh-CN: 按键读取必填配置值，缺失时抛出异常。
    /// en: Reads a required configuration value by key and throws when it is missing.
    /// </summary>
    public string GetRequired(string key) =>
        Get(key) ?? throw new InvalidOperationException($"Agent config '{key}' is required but not set");

    /// <summary>
    /// zh-CN: 从 JSON 文本解析配置，解析失败时回退为空配置。
    /// en: Parses configuration from JSON text and falls back to an empty config when parsing fails.
    /// </summary>
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
            catch
            {
                // zh-CN: 配置可能来自旧数据或第三方供应商，解析失败时保持空配置比阻断角色加载更安全。
                // en: Config can come from legacy data or third-party providers, so preserving an empty config is safer than blocking role loading.
            }
        }
        return config;
    }
}
