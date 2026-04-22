using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configuration;

/// <summary>
/// 对话会话补充索引配置。
/// Supplemental index configuration for chat sessions.
/// </summary>
public class ChatSessionIndexConfiguration : IEntityTypeConfiguration<ChatSession>
{
    /// <summary>
    /// 配置 <see cref="ChatSession"/> 的补充索引。
    /// Configures supplemental indexes for <see cref="ChatSession"/>.
    /// </summary>
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        // zh-CN: 这些索引补充常见的项目维度会话检索路径。
        // en: These indexes supplement the common session lookup paths scoped by project.
        builder.HasIndex(s => s.ProjectId)
            .HasDatabaseName("IX_ChatSessions_ProjectId");

        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("IX_ChatSessions_CreatedAt");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_ChatSessions_Status");
    }
}
