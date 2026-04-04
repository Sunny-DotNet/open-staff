using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InitialInput).IsRequired();
        builder.Property(e => e.ContextStrategy).HasMaxLength(20).HasDefaultValue(ContextStrategies.Full);

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ProjectId, e.CreatedAt });
        builder.HasIndex(e => e.Status);
    }
}

public class ChatFrameConfiguration : IEntityTypeConfiguration<ChatFrame>
{
    public void Configure(EntityTypeBuilder<ChatFrame> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InitiatorRole).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Purpose).IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Frames)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ParentFrame)
            .WithMany(f => f.ChildFrames)
            .HasForeignKey(e => e.ParentFrameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SessionId, e.Depth });
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Role).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(20).HasDefaultValue("text");

        builder.HasOne(e => e.Frame)
            .WithMany(f => f.Messages)
            .HasForeignKey(e => e.FrameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Session)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.FrameId, e.SequenceNo });
        builder.HasIndex(e => new { e.SessionId, e.SequenceNo });
    }
}

public class SessionEventConfiguration : IEntityTypeConfiguration<SessionEvent>
{
    public void Configure(EntityTypeBuilder<SessionEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasMaxLength(30).IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Events)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Frame)
            .WithMany()
            .HasForeignKey(e => e.FrameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.SessionId, e.SequenceNo }).IsUnique();
    }
}
