using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Entities;

namespace OpenStaff.EntityFrameworkCore.Configurations;

/// <summary>
/// 配置 MCP 服务目录实体的持久化映射。
/// Configures persistence for MCP server catalog entries.
/// </summary>
public class McpServerConfiguration : IEntityTypeConfiguration<McpServer>
{
    /// <summary>
    /// 配置 <see cref="McpServer"/> 实体。
    /// Configures the <see cref="McpServer"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<McpServer> builder)
    {
        builder.ToTable("McpServers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TransportType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Mode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DefaultConfig).HasColumnType("text");
        builder.Property(e => e.InstallInfo).HasColumnType("text");
        builder.Property(e => e.Icon).HasMaxLength(500);
        builder.Property(e => e.Homepage).HasMaxLength(500);
        builder.Property(e => e.NpmPackage).HasMaxLength(200);
        builder.Property(e => e.PypiPackage).HasMaxLength(200);
        builder.Property(e => e.MarketplaceUrl).HasMaxLength(500);

        builder.HasIndex(e => e.Source);
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.Mode);
        builder.HasIndex(e => e.Name);

        builder.HasMany(e => e.RoleBindings)
            .WithOne(binding => binding.McpServer)
            .HasForeignKey(binding => binding.McpServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// 配置角色测试场景与 MCP 服务器之间的绑定关系。
/// Configures bindings between role-test chat and installed MCP servers.
/// </summary>
public class AgentRoleMcpBindingConfiguration : IEntityTypeConfiguration<AgentRoleMcpBinding>
{
    /// <summary>
    /// 配置 <see cref="AgentRoleMcpBinding"/> 实体。
    /// Configures the <see cref="AgentRoleMcpBinding"/> entity.
    /// </summary>
    public void Configure(EntityTypeBuilder<AgentRoleMcpBinding> builder)
    {
        builder.ToTable("AgentRoleMcpBindings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ToolFilter).HasColumnType("text");
        builder.Property(e => e.IsEnabled).HasDefaultValue(true);

        builder.HasIndex(e => new { e.AgentRoleId, e.McpServerId }).IsUnique();
        builder.HasIndex(e => e.IsEnabled);

        builder.HasOne(e => e.AgentRole)
            .WithMany(role => role.McpBindings)
            .HasForeignKey(e => e.AgentRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.McpServer)
            .WithMany(server => server.RoleBindings)
            .HasForeignKey(e => e.McpServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
