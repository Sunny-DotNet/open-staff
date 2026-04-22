using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置插件元数据与程序集位置的持久化映射。
/// Configures persistence for plugin metadata and assembly locations.
/// </summary>
public class PluginConfiguration : IEntityTypeConfiguration<Plugin>
{
    /// <summary>
    /// 配置 <see cref="Plugin"/> 实体。
    /// Configures the <see cref="Plugin"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<Plugin> builder)
    {
        builder.ToTable("plugins");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Version).HasMaxLength(50);
        builder.Property(x => x.Manifest).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.AssemblyPath).HasMaxLength(1000);
    }
}
