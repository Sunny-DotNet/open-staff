using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置已安装 Skill 记录的持久化映射。
/// Configures persistence for installed skill records.
/// </summary>
public class InstalledSkillConfiguration : IEntityTypeConfiguration<InstalledSkill>
{
    /// <summary>
    /// 配置 <see cref="InstalledSkill"/> 实体。
    /// Configures the <see cref="InstalledSkill"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<InstalledSkill> builder)
    {
        builder.ToTable("InstalledSkills");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.InstallKey).HasMaxLength(400).IsRequired();
        builder.Property(e => e.SourceKey).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Scope).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Owner).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Repo).HasMaxLength(150).IsRequired();
        builder.Property(e => e.SkillId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.GithubUrl).HasMaxLength(500);
        builder.Property(e => e.InstallMode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InstallRootPath).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.TargetAgentsJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.RawMetadataJson).HasColumnType("text");
        builder.Property(e => e.IsEnabled).HasDefaultValue(true);

        builder.HasIndex(e => e.InstallKey).IsUnique();
        builder.HasIndex(e => new { e.Scope, e.ProjectId });
        builder.HasIndex(e => new { e.Owner, e.Repo, e.SkillId });
        builder.HasIndex(e => e.SourceKey);

        builder.HasOne(e => e.Project)
            .WithMany(project => project.InstalledSkills)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
