using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置项目检查点与其关联引用的持久化映射。
/// Configures persistence for project checkpoints and their related references.
/// </summary>
public class CheckpointConfiguration : IEntityTypeConfiguration<Checkpoint>
{
    /// <summary>
    /// 配置 <see cref="Checkpoint"/> 实体。
    /// Configures the <see cref="Checkpoint"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<Checkpoint> builder)
    {
        builder.ToTable("checkpoints");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GitCommitSha).HasMaxLength(40);
        builder.Property(x => x.FilesChanged).HasColumnType("TEXT");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Checkpoints)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // zh-CN: 任务或代理被移除后仍保留检查点审计记录，因此相关外键采用 SetNull。
        // en: Checkpoint audit records should survive task or agent removal, so those foreign keys use SetNull.
        builder.HasOne(x => x.ProjectAgentRole)
            .WithMany()
            .HasForeignKey(x => x.ProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
