using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Kanban.Api.Models;

namespace Kanban.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Column> Columns => Set<Column>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<CardTag> CardTags => Set<CardTag>();
    public DbSet<CardAssignment> CardAssignments => Set<CardAssignment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("pg_trgm");

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
        });

        builder.Entity<Project>(entity =>
        {
            entity.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Boards)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Members)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Tags)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Invitations)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.OwnerId);
        });

        builder.Entity<ProjectMember>(entity =>
        {
            entity.HasOne(x => x.User)
                .WithMany(x => x.ProjectMemberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
        });

        builder.Entity<Board>(entity =>
        {
            entity.HasOne(x => x.Project)
                .WithMany(x => x.Boards)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Columns)
                .WithOne(x => x.Board)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ProjectId);
        });

        builder.Entity<Column>(entity =>
        {
            entity.HasOne(x => x.Board)
                .WithMany(x => x.Columns)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Cards)
                .WithOne(x => x.Column)
                .HasForeignKey(x => x.ColumnId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.BoardId);
        });

        builder.Entity<Card>(entity =>
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

            entity.HasIndex(x => x.ColumnId);
            entity.HasIndex(x => x.CreatedBy);
            entity.HasIndex(x => x.Title)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            entity.HasIndex(x => x.Description)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
        });

        builder.Entity<Tag>(entity =>
        {
            entity.HasOne(x => x.Project)
                .WithMany(x => x.Tags)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.CardTags)
                .WithOne(x => x.Tag)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
        });

        builder.Entity<CardTag>(entity =>
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
        });

        builder.Entity<CardAssignment>(entity =>
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
        });

        builder.Entity<Attachment>(entity =>
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
        });

        builder.Entity<Subtask>(entity =>
        {
            entity.HasOne(x => x.Card)
                .WithMany(x => x.Subtasks)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.CardId);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CreatedBy);
            entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        });

        builder.Entity<Invitation>(entity =>
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
        });

        builder.Entity<Subscription>(entity =>
        {
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
            entity.HasIndex(x => new { x.UserId, x.EntityType, x.EntityId }).IsUnique();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.UserId);
        });

        builder.Entity<Project>().HasQueryFilter(x => x.DeletedAt == null);
        builder.Entity<Board>().HasQueryFilter(x => x.DeletedAt == null);
        builder.Entity<Column>().HasQueryFilter(x => x.DeletedAt == null);
        builder.Entity<Card>().HasQueryFilter(x => x.DeletedAt == null);
        builder.Entity<Attachment>().HasQueryFilter(x => x.DeletedAt == null);
        builder.Entity<Subtask>().HasQueryFilter(x => x.DeletedAt == null);
    }
}
