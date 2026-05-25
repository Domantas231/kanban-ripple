using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class SubtaskConfiguration : IEntityTypeConfiguration<Subtask>
{
    public void Configure(EntityTypeBuilder<Subtask> entity)
    {
        entity.HasOne(x => x.Card)
            .WithMany(x => x.Subtasks)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.CardId);

        entity.HasQueryFilter(x => x.DeletedAt == null);
    }
}
