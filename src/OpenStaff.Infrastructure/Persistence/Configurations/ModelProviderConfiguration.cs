using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class ModelProviderConfiguration : IEntityTypeConfiguration<ModelProvider>
{
    public void Configure(EntityTypeBuilder<ModelProvider> builder)
    {
        builder.ToTable("model_providers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProviderType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.BaseUrl).HasMaxLength(500);
        builder.Property(x => x.ApiKeyEncrypted).IsRequired();
        builder.Property(x => x.DefaultModel).HasMaxLength(200);
        builder.Property(x => x.ExtraConfig).HasColumnType("jsonb");
    }
}
