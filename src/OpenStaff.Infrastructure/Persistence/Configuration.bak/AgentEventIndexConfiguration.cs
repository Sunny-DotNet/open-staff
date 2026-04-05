using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configuration;

/// <summary>
/// AgentEvent entity index configuration
/// </summary>
public class AgentEventIndexConfiguration : IEntityTypeConfiguration<AgentEvent>
{
    public void Configure(EntityTypeBuilder<AgentEvent> builder)
    {
        // Composite index for project and agent queries
        builder.HasIndex(e => new { e.ProjectId, e.AgentId })
            .HasDatabaseName("IX_AgentEvents_ProjectId_AgentId");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_AgentEvents_CreatedAt");
    }
}
