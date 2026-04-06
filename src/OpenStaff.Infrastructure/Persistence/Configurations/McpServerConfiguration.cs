using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class McpServerConfiguration : IEntityTypeConfiguration<McpServer>
{
    public void Configure(EntityTypeBuilder<McpServer> builder)
    {
        builder.ToTable("McpServers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TransportType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DefaultConfig).HasColumnType("text");
        builder.Property(e => e.Icon).HasMaxLength(500);
        builder.Property(e => e.Homepage).HasMaxLength(500);
        builder.Property(e => e.NpmPackage).HasMaxLength(200);
        builder.Property(e => e.PypiPackage).HasMaxLength(200);
        builder.Property(e => e.MarketplaceUrl).HasMaxLength(500);

        builder.HasIndex(e => e.Source);
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.Name);

        builder.HasMany(e => e.Configs)
            .WithOne(c => c.McpServer)
            .HasForeignKey(c => c.McpServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class McpServerConfigConfiguration : IEntityTypeConfiguration<McpServerConfig>
{
    public void Configure(EntityTypeBuilder<McpServerConfig> builder)
    {
        builder.ToTable("McpServerConfigs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TransportType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ConnectionConfig).HasColumnType("text");
        builder.Property(e => e.EnvironmentVariables).HasColumnType("text");
        builder.Property(e => e.AuthConfig).HasColumnType("text");

        builder.HasIndex(e => e.McpServerId);
        builder.HasIndex(e => e.IsEnabled);
    }
}

public class AgentRoleMcpConfigConfiguration : IEntityTypeConfiguration<AgentRoleMcpConfig>
{
    public void Configure(EntityTypeBuilder<AgentRoleMcpConfig> builder)
    {
        builder.ToTable("AgentRoleMcpConfigs");
        builder.HasKey(e => new { e.AgentRoleId, e.McpServerConfigId });
        builder.Property(e => e.ToolFilter).HasColumnType("text");

        builder.HasOne(e => e.AgentRole)
            .WithMany()
            .HasForeignKey(e => e.AgentRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.McpServerConfig)
            .WithMany(c => c.AgentBindings)
            .HasForeignKey(e => e.McpServerConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
