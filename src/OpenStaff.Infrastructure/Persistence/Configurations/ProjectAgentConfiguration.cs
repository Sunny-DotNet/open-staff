using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class ProjectAgentConfiguration : IEntityTypeConfiguration<ProjectAgent>
{
    public void Configure(EntityTypeBuilder<ProjectAgent> builder)
    {
        builder.ToTable("project_agents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue(AgentStatus.Idle);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Agents)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AgentRole)
            .WithMany(x => x.ProjectAgents)
            .HasForeignKey(x => x.AgentRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
