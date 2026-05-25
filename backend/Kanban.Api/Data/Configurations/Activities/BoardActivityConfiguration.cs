using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations.Activities;

public sealed class BoardActivityConfiguration : IEntityTypeConfiguration<BoardActivity>
{
    public void Configure(EntityTypeBuilder<BoardActivity> entity)
    {
        entity.HasOne(x => x.Board)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(x => x.Action)
            .HasConversion(ActivityActionConverter.Instance)
            .IsRequired();

        entity.HasIndex(x => new { x.BoardId, x.CreatedAt });
    }
}
