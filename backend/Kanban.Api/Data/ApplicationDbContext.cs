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
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserGoogleAccount> UserGoogleAccounts => Set<UserGoogleAccount>();
    public DbSet<GoogleDriveLink> GoogleDriveLinks => Set<GoogleDriveLink>();
    public DbSet<PlannedBlock> PlannedBlocks => Set<PlannedBlock>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<CardActivity> CardActivities => Set<CardActivity>();
    public DbSet<ColumnActivity> ColumnActivities => Set<ColumnActivity>();
    public DbSet<BoardActivity> BoardActivities => Set<BoardActivity>();
    public DbSet<ProjectActivity> ProjectActivities => Set<ProjectActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Required by the GIN trigram indexes on Card.Title and Card.Description
        // (configured in CardConfiguration), which let Postgres serve ILIKE '%pattern%'
        // queries via index lookup instead of a sequential scan.
        builder.HasPostgresExtension("pg_trgm");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
