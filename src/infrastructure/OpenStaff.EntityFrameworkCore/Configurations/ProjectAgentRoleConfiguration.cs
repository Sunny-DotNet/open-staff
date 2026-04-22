using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置项目内角色关联与角色引用的持久化映射。
/// Configures persistence for project-scoped role memberships and their role references.
/// </summary>
public class ProjectAgentRoleConfiguration : IEntityTypeConfiguration<ProjectAgentRole>
{
    /// <summary>
    /// 配置 <see cref="ProjectAgentRole"/> 实体。
    /// Configures the <see cref="ProjectAgentRole"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<ProjectAgentRole> builder)
    {
        builder.ToTable("project_agent_roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue(AgentStatus.Idle);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.AgentRoles)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AgentRole)
            .WithMany(x => x.ProjectAgentRoles)
            .HasForeignKey(x => x.AgentRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SkillBindings)
            .WithOne(x => x.ProjectAgentRole)
            .HasForeignKey(x => x.ProjectAgentRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProjectId, x.AgentRoleId }).IsUnique();
    }
}
