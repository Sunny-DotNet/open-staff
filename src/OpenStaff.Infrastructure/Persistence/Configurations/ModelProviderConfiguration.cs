using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class ProviderAccountConfiguration : IEntityTypeConfiguration<ProviderAccount>
{
    public void Configure(EntityTypeBuilder<ProviderAccount> builder)
    {
        builder.ToTable("ProviderAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ProtocolType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EnvConfig).HasColumnName("EnvConfig").HasColumnType("text");

        builder.HasIndex(e => e.ProtocolType);
        builder.HasIndex(e => e.IsEnabled);
    }
}
