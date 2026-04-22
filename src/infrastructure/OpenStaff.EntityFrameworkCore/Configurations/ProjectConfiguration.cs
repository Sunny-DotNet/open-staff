using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置项目根实体及其默认生命周期字段。
/// Configures the project aggregate root and its default lifecycle fields.
/// </summary>
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    /// <summary>
    /// 配置 <see cref="Project"/> 实体。
    /// Configures the <see cref="Project"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

        // zh-CN: 语言、状态和阶段默认值与当前产品的中文优先引导流程保持一致。
        // en: Default language, status, and phase values align with the product's current Chinese-first onboarding workflow.
        builder.Property(x => x.Language).HasMaxLength(10).HasDefaultValue("zh-CN");
        builder.Property(x => x.WorkspacePath).HasMaxLength(1000);
        builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue(ProjectStatus.Initializing);
        builder.Property(x => x.Phase).HasMaxLength(50).HasDefaultValue(ProjectPhases.Brainstorming);
        builder.Property(x => x.Metadata).HasColumnType("TEXT");
        builder.Property(x => x.DefaultModelName).HasMaxLength(200);
        builder.Property(x => x.ExtraConfig).HasColumnType("TEXT");
    }
}
