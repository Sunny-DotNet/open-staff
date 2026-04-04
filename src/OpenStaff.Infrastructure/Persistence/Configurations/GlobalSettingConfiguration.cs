using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class GlobalSettingConfiguration : IEntityTypeConfiguration<GlobalSetting>
{
    public void Configure(EntityTypeBuilder<GlobalSetting> builder)
    {
        builder.ToTable("global_settings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Value).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.Category).HasMaxLength(100);
    }
}
