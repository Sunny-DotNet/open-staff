using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configuration;

/// <summary>
/// Project entity index configuration
/// </summary>
public class ProjectIndexConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        // Add indexes for commonly queried fields
        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_Projects_Status");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("IX_Projects_CreatedAt");

        builder.HasIndex(p => p.UpdatedAt)
            .HasDatabaseName("IX_Projects_UpdatedAt");
    }
}
