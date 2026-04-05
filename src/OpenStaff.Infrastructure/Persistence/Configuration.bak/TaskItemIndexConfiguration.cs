using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configuration;

/// <summary>
/// TaskItem entity index configuration
/// </summary>
public class TaskItemIndexConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // Index for project task queries
        builder.HasIndex(t => t.ProjectId)
            .HasDatabaseName("IX_TaskItems_ProjectId");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_TaskItems_Status");

        builder.HasIndex(t => t.Priority)
            .HasDatabaseName("IX_TaskItems_Priority");
    }
}
