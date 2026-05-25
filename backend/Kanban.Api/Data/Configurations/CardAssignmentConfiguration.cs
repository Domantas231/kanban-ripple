using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class CardAssignmentConfiguration : IEntityTypeConfiguration<CardAssignment>
{
    public void Configure(EntityTypeBuilder<CardAssignment> entity)
    {
        entity.HasOne(x => x.Card)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Assigner)
            .WithMany()
            .HasForeignKey(x => x.AssignedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => x.CardId);
        entity.HasIndex(x => x.UserId);
        entity.HasIndex(x => x.AssignedBy);
        entity.HasIndex(x => new { x.CardId, x.UserId }).IsUnique();
    }
}
