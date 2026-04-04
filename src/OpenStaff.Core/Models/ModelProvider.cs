namespace OpenStaff.Core.Models;

/// <summary>
/// 模型供应商配置 / Model provider configuration
/// </summary>
public class ModelProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public string ApiKeyMode { get; set; } = ApiKeyModes.EnvVar; // input / env / device
    public string? ApiKeyEnvVar { get; set; } // 环境变量名
    public string? DefaultModel { get; set; }
    public string? ExtraConfig { get; set; }
    public bool IsEnabled { get; set; } = false; // 是否启用
    public bool IsBuiltin { get; set; } = false; // 是否为内置供应商
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class ProviderTypes
{
    public const string OpenAI = "openai";
    public const string Google = "google";
    public const string Anthropic = "anthropic";
    public const string GitHubCopilot = "github_copilot";
    public const string AzureOpenAI = "azure_openai";
    public const string GenericOpenAI = "generic_openai";
}

public static class ApiKeyModes
{
    public const string Input = "input";
    public const string EnvVar = "env";
    public const string Device = "device"; // GitHub 设备码授权
}
