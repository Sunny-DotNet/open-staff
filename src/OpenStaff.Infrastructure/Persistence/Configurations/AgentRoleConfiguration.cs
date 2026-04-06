using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class AgentRoleConfiguration : IEntityTypeConfiguration<AgentRole>
{
    public void Configure(EntityTypeBuilder<AgentRole> builder)
    {
        builder.ToTable("agent_roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.RoleType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ModelName).HasMaxLength(200);
        builder.Property(x => x.Config).HasColumnType("TEXT");

        builder.OwnsOne(x => x.Soul, soul =>
        {
            soul.ToJson();
        });

        builder.Ignore(x => x.ProviderAccount);

        builder.HasOne(x => x.Plugin)
            .WithMany(x => x.AgentRoles)
            .HasForeignKey(x => x.PluginId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
