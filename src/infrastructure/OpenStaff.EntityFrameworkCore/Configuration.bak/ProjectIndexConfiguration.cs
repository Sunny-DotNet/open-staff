using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configuration;

/// <summary>
/// 项目补充索引配置。
/// Supplemental index configuration for projects.
/// </summary>
public class ProjectIndexConfiguration : IEntityTypeConfiguration<Project>
{
    /// <summary>
    /// 配置 <see cref="Project"/> 的补充索引。
    /// Configures supplemental indexes for <see cref="Project"/>.
    /// </summary>
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        // zh-CN: 这些索引覆盖按生命周期字段检索项目列表的常见路径。
        // en: These indexes cover the common query paths that list projects by lifecycle fields.
        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_Projects_Status");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("IX_Projects_CreatedAt");

        builder.HasIndex(p => p.UpdatedAt)
            .HasDatabaseName("IX_Projects_UpdatedAt");
    }
}
