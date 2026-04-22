using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置对话会话根记录的持久化映射。
/// Configures persistence mappings for chat session roots.
/// </summary>
public class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    /// <summary>
    /// 配置 <see cref="ChatSession"/> 实体。
    /// Configures the <see cref="ChatSession"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InitialInput).IsRequired();
        builder.Property(e => e.ContextStrategy).HasMaxLength(20).HasDefaultValue(ContextStrategies.Full);
        builder.Property(e => e.Scene).HasMaxLength(50).HasDefaultValue(SessionSceneTypes.ProjectBrainstorm);

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // zh-CN: 会话列表通常按项目、场景和创建时间过滤，因此预建组合索引以支持最近会话检索。
        // en: Session lists are commonly filtered by project, scene, and creation time, so composite indexes are prebuilt for recent-session lookups.
        builder.HasIndex(e => e.ProjectId)
            .HasDatabaseName("IX_ChatSessions_ProjectId");
        builder.HasIndex(e => new { e.ProjectId, e.CreatedAt });
        builder.HasIndex(e => new { e.ProjectId, e.Scene, e.CreatedAt });
        builder.HasIndex(e => e.Status);
    }
}

/// <summary>
/// 配置对话帧的层级关系与查询索引。
/// Configures hierarchy and query indexes for chat frames.
/// </summary>
public class ChatFrameConfiguration : IEntityTypeConfiguration<ChatFrame>
{
    /// <summary>
    /// 配置 <see cref="ChatFrame"/> 实体。
    /// Configures the <see cref="ChatFrame"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<ChatFrame> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Purpose).IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Frames)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ExecutionPackage)
            .WithMany(p => p.Frames)
            .HasForeignKey(e => e.ExecutionPackageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ParentFrame)
            .WithMany(f => f.ChildFrames)
            .HasForeignKey(e => e.ParentFrameId)
            .OnDelete(DeleteBehavior.Restrict);

        // zh-CN: 父子帧链路使用 Restrict，避免删除父帧时级联抹除整段执行栈历史。
        // en: Parent/child frame links use Restrict so deleting a parent does not cascade through the entire execution stack history.
        builder.HasOne(e => e.Task)
            .WithMany(t => t.Frames)
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AgentRole>()
            .WithMany()
            .HasForeignKey(e => e.InitiatorAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProjectAgentRole>()
            .WithMany()
            .HasForeignKey(e => e.InitiatorProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AgentRole>()
            .WithMany()
            .HasForeignKey(e => e.TargetAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProjectAgentRole>()
            .WithMany()
            .HasForeignKey(e => e.TargetProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.SessionId, e.Depth });
        builder.HasIndex(e => e.TaskId);
    }
}

/// <summary>
/// 配置对话消息的层级关系与顺序索引。
/// Configures hierarchy and ordering indexes for chat messages.
/// </summary>
public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    /// <summary>
    /// 配置 <see cref="ChatMessage"/> 实体。
    /// Configures the <see cref="ChatMessage"/> entity.
    /// </summary>
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

        builder.HasOne(e => e.ExecutionPackage)
            .WithMany(p => p.Messages)
            .HasForeignKey(e => e.ExecutionPackageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.OriginatingFrame)
            .WithMany()
            .HasForeignKey(e => e.OriginatingFrameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ParentMessage)
            .WithMany(e => e.ChildMessages)
            .HasForeignKey(e => e.ParentMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AgentRole>()
            .WithMany()
            .HasForeignKey(e => e.AgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProjectAgentRole>()
            .WithMany()
            .HasForeignKey(e => e.ProjectAgentRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // zh-CN: 消息顺序号在帧内和会话内都需要稳定递增，双索引可同时覆盖局部和全局回放。
        // en: Message sequence numbers must stay stable both within a frame and across a session, so dual indexes support local and global replay.
        builder.HasIndex(e => new { e.FrameId, e.SequenceNo });
        builder.HasIndex(e => new { e.SessionId, e.SequenceNo });
        builder.HasIndex(e => e.ParentMessageId);
        builder.HasIndex(e => e.ExecutionPackageId);
    }
}

/// <summary>
/// 配置会话事件流的持久化映射。
/// Configures persistence mappings for the session event stream.
/// </summary>
public class SessionEventConfiguration : IEntityTypeConfiguration<SessionEvent>
{
    /// <summary>
    /// 配置 <see cref="SessionEvent"/> 实体。
    /// Configures the <see cref="SessionEvent"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<SessionEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasMaxLength(30).IsRequired();

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Events)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ExecutionPackage)
            .WithMany(p => p.Events)
            .HasForeignKey(e => e.ExecutionPackageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Frame)
            .WithMany()
            .HasForeignKey(e => e.FrameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Message)
            .WithMany()
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.SetNull);

        // zh-CN: 每个会话内的 SequenceNo 必须唯一，流式重放才能严格按事件产生顺序恢复。
        // en: SequenceNo must be unique within a session so stream replay can restore events in their exact creation order.
        builder.HasIndex(e => new { e.SessionId, e.SequenceNo }).IsUnique();
        builder.HasIndex(e => e.MessageId);
        builder.HasIndex(e => e.ExecutionPackageId);
    }
}
