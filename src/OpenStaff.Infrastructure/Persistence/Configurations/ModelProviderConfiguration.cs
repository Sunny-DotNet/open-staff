using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class ModelProviderConfiguration : IEntityTypeConfiguration<ModelProvider>
{
    public void Configure(EntityTypeBuilder<ModelProvider> builder)
    {
        builder.ToTable("ModelProviders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ProviderType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.BaseUrl).HasMaxLength(500);
        builder.Property(e => e.ApiKeyEncrypted).HasMaxLength(2000);
        builder.Property(e => e.ApiKeyMode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ApiKeyEnvVar).HasMaxLength(200);
        builder.Property(e => e.DefaultModel).HasMaxLength(200);
        builder.Property(e => e.ExtraConfig).HasColumnType("text");

        builder.HasIndex(e => e.ProviderType);
        builder.HasIndex(e => e.IsEnabled);
    }
}
