namespace OpenStaff.Core.Models;

/// <summary>
/// 供应商账户 — 一个协议类型可以创建多个账户
/// </summary>
public class ProviderAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = string.Empty; // IProtocol.ProviderName
    public string EnvConfig { get; set; } = string.Empty; // ProtocolEnv JSON（[Encrypted] 字段单独加密）
    public bool IsEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
