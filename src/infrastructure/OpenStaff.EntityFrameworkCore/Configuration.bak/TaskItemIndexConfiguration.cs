using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configuration;

/// <summary>
/// 任务补充索引配置。
/// Supplemental index configuration for task items.
/// </summary>
public class TaskItemIndexConfiguration : IEntityTypeConfiguration<TaskItem>
{
    /// <summary>
    /// 配置 <see cref="TaskItem"/> 的补充索引。
    /// Configures supplemental indexes for <see cref="TaskItem"/>.
    /// </summary>
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // zh-CN: 这些索引补充任务看板按项目、状态和优先级筛选的读取路径。
        // en: These indexes supplement task-board queries filtered by project, status, and priority.
        builder.HasIndex(t => t.ProjectId)
            .HasDatabaseName("IX_TaskItems_ProjectId");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_TaskItems_Status");

        builder.HasIndex(t => t.Priority)
            .HasDatabaseName("IX_TaskItems_Priority");
    }
}
