using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configuration;

/// <summary>
/// ChatSession entity index configuration
/// </summary>
public class ChatSessionIndexConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        // Index for project session queries
        builder.HasIndex(s => s.ProjectId)
            .HasDatabaseName("IX_ChatSessions_ProjectId");

        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("IX_ChatSessions_CreatedAt");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_ChatSessions_Status");
    }
}
