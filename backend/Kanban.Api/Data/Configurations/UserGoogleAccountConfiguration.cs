using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Api.Data.Configurations;

public sealed class UserGoogleAccountConfiguration : IEntityTypeConfiguration<UserGoogleAccount>
{
    public void Configure(EntityTypeBuilder<UserGoogleAccount> entity)
    {
        entity.HasKey(x => x.Id);

        entity.HasOne(x => x.User)
            .WithOne(x => x.GoogleAccount)
            .HasForeignKey<UserGoogleAccount>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.UserId).IsUnique();

        entity.Property(x => x.GoogleEmail).IsRequired();
        entity.Property(x => x.GoogleUserId).IsRequired();
        entity.Property(x => x.EncryptedAccessToken).IsRequired();
        entity.Property(x => x.EncryptedRefreshToken).IsRequired();
    }
}
