using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> entity)
    {
        entity.HasOne(x => x.Project)
            .WithMany(x => x.Invitations)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Inviter)
            .WithMany()
            .HasForeignKey(x => x.InvitedBy)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Accepter)
            .WithMany()
            .HasForeignKey(x => x.AcceptedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => x.ProjectId);
        entity.HasIndex(x => x.InvitedBy);
        entity.HasIndex(x => x.AcceptedBy);
        entity.HasIndex(x => x.Email);
        entity.HasIndex(x => x.Token).IsUnique();
        entity.HasIndex(x => x.ExpiresAt);
    }
}
