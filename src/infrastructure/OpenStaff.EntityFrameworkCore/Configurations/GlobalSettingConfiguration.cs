using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置全局设置键值表的持久化映射。
/// Configures persistence for the global settings key-value table.
/// </summary>
public class GlobalSettingConfiguration : IEntityTypeConfiguration<GlobalSetting>
{
    /// <summary>
    /// 配置 <see cref="GlobalSetting"/> 实体。
    /// Configures the <see cref="GlobalSetting"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<GlobalSetting> builder)
    {
        builder.ToTable("global_settings");
        builder.HasKey(x => x.Id);

        // zh-CN: Key 在业务上充当逻辑主键，唯一索引用于阻止同名配置被重复写入。
        // en: Key acts as the business identifier, so a unique index prevents duplicate settings with the same name.
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Value).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.Category).HasMaxLength(100);
    }
}
