using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> entity)
    {
        entity.HasOne(x => x.Project)
            .WithMany(x => x.Boards)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Columns)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Tags)
            .WithOne(x => x.Board)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.ProjectId);
        entity.HasIndex(x => new { x.ProjectId, x.Name })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        entity.HasQueryFilter(x => x.DeletedAt == null);
    }
}
