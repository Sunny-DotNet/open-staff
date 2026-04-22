using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置项目内角色关联与 skill 之间的绑定关系。
/// Configures bindings between project-scoped role memberships and managed skills.
/// </summary>
public class ProjectAgentRoleSkillBindingConfiguration : IEntityTypeConfiguration<ProjectAgentRoleSkillBinding>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProjectAgentRoleSkillBinding> builder)
    {
        builder.ToTable("ProjectAgentRoleSkillBindings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SkillInstallKey).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SkillId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Owner).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Repo).HasMaxLength(150).IsRequired();
        builder.Property(e => e.GithubUrl).HasMaxLength(500);
        builder.Property(e => e.IsEnabled).HasDefaultValue(true);

        builder.HasIndex(e => new { e.ProjectAgentRoleId, e.SkillInstallKey }).IsUnique();
        builder.HasIndex(e => e.IsEnabled);

        builder.HasOne(e => e.ProjectAgentRole)
            .WithMany(agent => agent.SkillBindings)
            .HasForeignKey(e => e.ProjectAgentRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// 配置角色测试场景与 skill 之间的绑定关系。
/// Configures bindings between role-test chat and managed skills.
/// </summary>
public class AgentRoleSkillBindingConfiguration : IEntityTypeConfiguration<AgentRoleSkillBinding>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AgentRoleSkillBinding> builder)
    {
        builder.ToTable("AgentRoleSkillBindings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SkillInstallKey).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SkillId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Owner).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Repo).HasMaxLength(150).IsRequired();
        builder.Property(e => e.GithubUrl).HasMaxLength(500);
        builder.Property(e => e.IsEnabled).HasDefaultValue(true);

        builder.HasIndex(e => new { e.AgentRoleId, e.SkillInstallKey }).IsUnique();
        builder.HasIndex(e => e.IsEnabled);

        builder.HasOne(e => e.AgentRole)
            .WithMany(role => role.SkillBindings)
            .HasForeignKey(e => e.AgentRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
