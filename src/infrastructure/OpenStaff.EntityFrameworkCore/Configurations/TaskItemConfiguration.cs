using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置任务实体、层级关系与代理分配引用。
/// Configures tasks, their hierarchy, and agent assignment references.
/// </summary>
public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    /// <summary>
    /// 配置 <see cref="TaskItem"/> 实体。
    /// Configures the <see cref="TaskItem"/> entity.
    /// </summary>
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

        builder.HasOne(x => x.AssignedProjectAgentRole)
            .WithMany(x => x.AssignedTasks)
            .HasForeignKey(x => x.AssignedProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // zh-CN: 父任务链路使用 Restrict，防止误删父任务时递归级联抹除整个子任务树。
        // en: Parent-task links use Restrict so deleting a parent does not recursively wipe the full subtask tree by mistake.
        builder.HasOne(x => x.ParentTask)
            .WithMany(x => x.SubTasks)
            .HasForeignKey(x => x.ParentTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
