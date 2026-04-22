using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置代理事件实体的持久化映射与时间线索引。
/// Configures persistence mappings and timeline indexes for agent events.
/// </summary>
public class AgentEventConfiguration : IEntityTypeConfiguration<AgentEvent>
{
    /// <summary>
    /// 配置 <see cref="AgentEvent"/> 实体。
    /// Configures the <see cref="AgentEvent"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<AgentEvent> builder)
    {
        builder.ToTable("agent_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Metadata).HasColumnType("TEXT");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectAgentRole)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.ProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentEvent)
            .WithMany(x => x.ChildEvents)
            .HasForeignKey(x => x.ParentEventId)
            .OnDelete(DeleteBehavior.Restrict);

        // zh-CN: 事件流通常按项目/代理分区后再按时间倒序读取，因此 CreatedAt 放在降序列中。
        // en: Event streams are usually partitioned by project or agent and then read newest-first, so CreatedAt is stored as the descending portion of the composite index.
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(x => new { x.ProjectAgentRoleId, x.CreatedAt }).IsDescending(false, true);
    }
}
