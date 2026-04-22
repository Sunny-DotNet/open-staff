using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置供应商账户与其环境配置序列化字段。
/// Configures provider accounts and their serialized environment settings.
/// </summary>
public class ProviderAccountConfiguration : IEntityTypeConfiguration<ProviderAccount>
{
    /// <summary>
    /// 配置 <see cref="ProviderAccount"/> 实体。
    /// Configures the <see cref="ProviderAccount"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<ProviderAccount> builder)
    {
        builder.ToTable("ProviderAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ProtocolType).HasMaxLength(50).IsRequired();

        builder.HasIndex(e => e.ProtocolType);
        builder.HasIndex(e => e.IsEnabled);
    }
}
