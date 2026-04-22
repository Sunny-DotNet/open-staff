using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置任务依赖边的持久化映射。
/// Configures persistence for task dependency edges.
/// </summary>
public class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    /// <summary>
    /// 配置 <see cref="TaskDependency"/> 实体。
    /// Configures the <see cref="TaskDependency"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.ToTable("task_dependencies");

        // zh-CN: 依赖边现在使用继承的 Id 作为主键，同时保留原自然键唯一性，避免重复依赖边。
        // en: Dependency edges now use the inherited Id as the primary key while preserving uniqueness of the original natural key.
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TaskId, x.DependsOnId }).IsUnique();
        builder.HasIndex(x => x.DependsOnId);

        // zh-CN: 两侧都采用 Cascade，删除任一任务时都能自动清理关联依赖边，避免孤立图边。
        // en: Both sides cascade so removing either task automatically cleans up the related dependency edge and prevents orphaned graph links.
        builder.HasOne(x => x.Task)
            .WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DependsOn)
            .WithMany(x => x.Dependents)
            .HasForeignKey(x => x.DependsOnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
