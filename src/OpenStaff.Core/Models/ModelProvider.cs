namespace OpenStaff.Core.Models;

/// <summary>
/// 模型供应商配置 / Model provider configuration
/// </summary>
public class ModelProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // 供应商名称
    public string ProviderType { get; set; } = string.Empty; // openai/azure_openai/generic_openai
    public string? BaseUrl { get; set; } // API 端点
    public string ApiKeyEncrypted { get; set; } = string.Empty; // 加密的 API Key
    public string? DefaultModel { get; set; } // 默认模型名
    public string? ExtraConfig { get; set; } // JSON 额外配置
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentRole> AgentRoles { get; set; } = new List<AgentRole>();
}

public static class ProviderTypes
{
    public const string OpenAI = "openai";
    public const string AzureOpenAI = "azure_openai";
    public const string GenericOpenAI = "generic_openai"; // 国内厂商等兼容接口
}
