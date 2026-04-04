using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class PluginConfiguration : IEntityTypeConfiguration<Plugin>
{
    public void Configure(EntityTypeBuilder<Plugin> builder)
    {
        builder.ToTable("plugins");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Version).HasMaxLength(50);
        builder.Property(x => x.Manifest).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.AssemblyPath).HasMaxLength(1000);
    }
}
