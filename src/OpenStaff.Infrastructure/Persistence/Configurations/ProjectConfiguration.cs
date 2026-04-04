using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.TechStack).HasColumnType("jsonb");
        builder.Property(x => x.Language).HasMaxLength(10).HasDefaultValue("zh-CN");
        builder.Property(x => x.WorkspacePath).HasMaxLength(1000);
        builder.Property(x => x.GitConfig).HasColumnType("jsonb");
        builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue(ProjectStatus.Initializing);
        builder.Property(x => x.Metadata).HasColumnType("jsonb");
    }
}
