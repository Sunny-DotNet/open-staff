using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configuration;

/// <summary>
/// 代理事件补充索引配置。
/// Supplemental index configuration for agent events.
/// </summary>
public class AgentEventIndexConfiguration : IEntityTypeConfiguration<AgentEvent>
{
    /// <summary>
    /// 配置 <see cref="AgentEvent"/> 的补充索引。
    /// Configures supplemental indexes for <see cref="AgentEvent"/>.
    /// </summary>
    public void Configure(EntityTypeBuilder<AgentEvent> builder)
    {
        // zh-CN: 补充索引用于覆盖按项目与代理联合过滤的旧查询路径。
        // en: These supplemental indexes cover legacy query paths that filter by project and agent together.
        builder.HasIndex(e => new { e.ProjectId, e.ProjectAgentRoleId })
            .HasDatabaseName("IX_AgentEvents_ProjectId_ProjectAgentRoleId");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_AgentEvents_CreatedAt");
    }
}
