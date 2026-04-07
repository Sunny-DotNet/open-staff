namespace OpenStaff.Agent;

/// <summary>
/// 智能体配置 Schema — 描述该供应商需要哪些配置字段（前端据此渲染动态表单）
/// </summary>
public class AgentConfigSchema
{
    /// <summary>供应商标识</summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>描述说明</summary>
    public string? Description { get; set; }

    /// <summary>配置字段列表</summary>
    public List<AgentConfigField> Fields { get; set; } = [];
}

/// <summary>
/// 单个配置字段定义
/// </summary>
public class AgentConfigField
{
    /// <summary>字段键名（如 "apiKey", "model"）</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>显示标签</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>字段类型（text, password, select, number）</summary>
    public string FieldType { get; set; } = "text";

    /// <summary>是否必填</summary>
    public bool Required { get; set; }

    /// <summary>默认值</summary>
    public string? DefaultValue { get; set; }

    /// <summary>占位提示</summary>
    public string? Placeholder { get; set; }

    /// <summary>select 类型的可选项</summary>
    public List<AgentConfigOption>? Options { get; set; }
}

/// <summary>
/// 下拉选项
/// </summary>
public class AgentConfigOption
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
