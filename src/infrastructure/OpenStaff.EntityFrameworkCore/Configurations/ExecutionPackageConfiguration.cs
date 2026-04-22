using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置执行包及任务投影索引的持久化映射。
/// Configures persistence mappings for execution packages and task projection links.
/// </summary>
public sealed class ExecutionPackageConfiguration : IEntityTypeConfiguration<ExecutionPackage>
{
    /// <summary>
    /// 配置 <see cref="ExecutionPackage"/> 实体。
    /// Configures the <see cref="ExecutionPackage"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<ExecutionPackage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntryKind).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PackageKind).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Scene).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentExecutionPackage)
            .WithMany(x => x.ChildExecutionPackages)
            .HasForeignKey(x => x.ParentExecutionPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RetryOfExecutionPackage)
            .WithMany(x => x.RetryExecutionPackages)
            .HasForeignKey(x => x.RetryOfExecutionPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RootFrame)
            .WithMany()
            .HasForeignKey(x => x.RootFrameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SourceFrame)
            .WithMany()
            .HasForeignKey(x => x.SourceFrameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AgentRole>()
            .WithMany()
            .HasForeignKey(x => x.AgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProjectAgentRole>()
            .WithMany()
            .HasForeignKey(x => x.ProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AgentRole>()
            .WithMany()
            .HasForeignKey(x => x.InitiatorAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProjectAgentRole>()
            .WithMany()
            .HasForeignKey(x => x.InitiatorProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AgentRole>()
            .WithMany()
            .HasForeignKey(x => x.TargetAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProjectAgentRole>()
            .WithMany()
            .HasForeignKey(x => x.TargetProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // zh-CN: 会话调试、最近执行列表和项目级筛选都会按 session/status/time 组合查询，所以这里预建常用索引。
        // 例如打开某个项目的脑暴会话时，需要很快拿到该 session 下最近一次 entry package。
        // en: Prebuild the common session/status/time indexes used by debugging and recent-execution queries.
        builder.HasIndex(x => new { x.SessionId, x.CreatedAt });
        builder.HasIndex(x => new { x.SessionId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.ProjectId, x.EntryKind, x.CreatedAt });
        builder.HasIndex(x => x.RootFrameId);
        builder.HasIndex(x => x.TaskId);
    }
}

/// <summary>
/// 配置任务与执行包之间的投影索引映射。
/// Configures persistence mappings for task-to-execution-package projection links.
/// </summary>
public sealed class TaskExecutionLinkConfiguration : IEntityTypeConfiguration<TaskExecutionLink>
{
    /// <summary>
    /// 配置 <see cref="TaskExecutionLink"/> 实体。
    /// Configures the <see cref="TaskExecutionLink"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<TaskExecutionLink> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Task)
            .WithMany(x => x.ExecutionLinks)
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ExecutionPackage)
            .WithMany(x => x.TaskExecutionLinks)
            .HasForeignKey(x => x.ExecutionPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TaskId, x.CreatedAt });
        builder.HasIndex(x => new { x.ExecutionPackageId, x.SourceEffectIndex });
    }
}
