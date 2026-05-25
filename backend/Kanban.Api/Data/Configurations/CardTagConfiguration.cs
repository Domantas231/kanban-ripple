using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class CardTagConfiguration : IEntityTypeConfiguration<CardTag>
{
    public void Configure(EntityTypeBuilder<CardTag> entity)
    {
        entity.HasOne(x => x.Card)
            .WithMany(x => x.CardTags)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Tag)
            .WithMany(x => x.CardTags)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.CardId);
        entity.HasIndex(x => x.TagId);
        entity.HasIndex(x => new { x.CardId, x.TagId }).IsUnique();
    }
}
