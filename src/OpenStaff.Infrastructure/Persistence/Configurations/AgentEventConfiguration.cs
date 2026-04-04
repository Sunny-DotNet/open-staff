using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class AgentEventConfiguration : IEntityTypeConfiguration<AgentEvent>
{
    public void Configure(EntityTypeBuilder<AgentEvent> builder)
    {
        builder.ToTable("agent_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Metadata).HasColumnType("jsonb");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Agent)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentEvent)
            .WithMany(x => x.ChildEvents)
            .HasForeignKey(x => x.ParentEventId)
            .OnDelete(DeleteBehavior.Restrict);

        // 时间线查询索引
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(x => new { x.AgentId, x.CreatedAt }).IsDescending(false, true);
    }
}
