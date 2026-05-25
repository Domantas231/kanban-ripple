using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations.Activities;

public sealed class ColumnActivityConfiguration : IEntityTypeConfiguration<ColumnActivity>
{
    public void Configure(EntityTypeBuilder<ColumnActivity> entity)
    {
        entity.HasOne(x => x.Column)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(x => x.Action)
            .HasConversion(ActivityActionConverter.Instance)
            .IsRequired();

        entity.HasIndex(x => new { x.ColumnId, x.CreatedAt });
    }
}
