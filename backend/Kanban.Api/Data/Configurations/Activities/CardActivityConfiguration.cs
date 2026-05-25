using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations.Activities;

public sealed class CardActivityConfiguration : IEntityTypeConfiguration<CardActivity>
{
    public void Configure(EntityTypeBuilder<CardActivity> entity)
    {
        entity.HasOne(x => x.Card)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(x => x.Action)
            .HasConversion(ActivityActionConverter.Instance)
            .IsRequired();

        entity.HasIndex(x => new { x.CardId, x.CreatedAt });
    }
}
