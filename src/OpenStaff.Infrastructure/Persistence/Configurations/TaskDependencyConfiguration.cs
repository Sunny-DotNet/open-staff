using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence.Configurations;

public class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.ToTable("task_dependencies");
        builder.HasKey(x => new { x.TaskId, x.DependsOnId });

        builder.HasOne(x => x.Task)
            .WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DependsOn)
            .WithMany(x => x.Dependents)
            .HasForeignKey(x => x.DependsOnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
