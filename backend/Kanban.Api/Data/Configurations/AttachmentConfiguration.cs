using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> entity)
    {
        entity.HasOne(x => x.Card)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Uploader)
            .WithMany(x => x.UploadedAttachments)
            .HasForeignKey(x => x.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => x.CardId);
        entity.HasIndex(x => x.UploadedBy);

        entity.HasQueryFilter(x => x.DeletedAt == null);
    }
}
