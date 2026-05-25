using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations.Activities;

public sealed class ProjectActivityConfiguration : IEntityTypeConfiguration<ProjectActivity>
{
    public void Configure(EntityTypeBuilder<ProjectActivity> entity)
    {
        entity.HasOne(x => x.Project)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(x => x.Action)
            .HasConversion(ActivityActionConverter.Instance)
            .IsRequired();

        entity.HasIndex(x => new { x.ProjectId, x.CreatedAt });
    }
}
