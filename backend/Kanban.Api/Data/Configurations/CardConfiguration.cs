using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> entity)
    {
        entity.Property(x => x.Version).IsConcurrencyToken();

        entity.HasOne(x => x.Column)
            .WithMany(x => x.Cards)
            .HasForeignKey(x => x.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Creator)
            .WithMany(x => x.CreatedCards)
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasMany(x => x.CardTags)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Assignments)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Attachments)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Subtasks)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Comments)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.ColumnId);
        entity.HasIndex(x => x.CreatedBy);
        entity.HasIndex(x => x.Title)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        entity.HasIndex(x => x.Description)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        entity.HasQueryFilter(x => x.DeletedAt == null);
    }
}
