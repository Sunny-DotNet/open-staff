using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue(TaskItemStatus.Pending);
        builder.Property(x => x.Metadata).HasColumnType("TEXT");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Tasks)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AssignedAgent)
            .WithMany(x => x.AssignedTasks)
            .HasForeignKey(x => x.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentTask)
            .WithMany(x => x.SubTasks)
            .HasForeignKey(x => x.ParentTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
