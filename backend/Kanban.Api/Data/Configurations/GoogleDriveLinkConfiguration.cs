using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class GoogleDriveLinkConfiguration : IEntityTypeConfiguration<GoogleDriveLink>
{
    public void Configure(EntityTypeBuilder<GoogleDriveLink> entity)
    {
        entity.HasKey(x => x.Id);

        entity.HasOne(x => x.Card)
            .WithMany(x => x.GoogleDriveLinks)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Linker)
            .WithMany()
            .HasForeignKey(x => x.LinkedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.Property(x => x.GoogleFileId).IsRequired();
        entity.Property(x => x.Name).IsRequired();
        entity.Property(x => x.MimeType).IsRequired();
        entity.Property(x => x.WebViewLink).IsRequired();

        entity.Property(x => x.SharePermission)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<GoogleDriveSharePermission>(v, true))
            .IsRequired();

        entity.HasIndex(x => x.CardId)
            .HasFilter("\"DeletedAt\" IS NULL");

        entity.HasIndex(x => new { x.CardId, x.GoogleFileId })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        entity.HasQueryFilter(x => x.DeletedAt == null);
    }
}
