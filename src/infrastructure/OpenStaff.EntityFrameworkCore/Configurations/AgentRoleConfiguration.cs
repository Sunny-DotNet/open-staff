using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置代理角色实体与其扩展值对象的持久化方式。
/// Configures persistence for agent roles and their extended value objects.
/// </summary>
public class AgentRoleConfiguration : IEntityTypeConfiguration<AgentRole>
{
    /// <summary>
    /// 配置 <see cref="AgentRole"/> 实体。
    /// Configures the <see cref="AgentRole"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<AgentRole> builder)
    {
        builder.ToTable("agent_roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ModelName).HasMaxLength(200);
        builder.Property(x => x.Config).HasColumnType("TEXT");
        builder.Property(x => x.ProviderType).HasMaxLength(50);

        // zh-CN: Soul 作为值对象直接序列化进角色记录，避免拆成额外表后增加读取成本。
        // en: Persist Soul as an owned JSON value inside the role row to avoid the extra joins of a separate table.
        builder.OwnsOne(x => x.Soul, soul =>
        {
            soul.ToJson();
        });

        builder.HasOne(x => x.Plugin)
            .WithMany(x => x.AgentRoles)
            .HasForeignKey(x => x.PluginId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
