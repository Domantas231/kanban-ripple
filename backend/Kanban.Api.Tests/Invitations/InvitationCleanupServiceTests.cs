using System.Reflection;
using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Invitations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Tests.Invitations;

public sealed class InvitationCleanupServiceTests
{
    [Fact]
    public async Task DeleteExpiredInvitationsAsync_DeletesExpiredPendingInvitations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));

        await using var provider = services.BuildServiceProvider();

        Guid expiredPendingId;
        Guid activePendingId;

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            (expiredPendingId, activePendingId, _) = await SeedInvitationsAsync(dbContext);
        }

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger = provider.GetRequiredService<ILogger<InvitationCleanupService>>();
        var service = new InvitationCleanupService(scopeFactory, logger, Options.Create(new InvitationOptions()));

        await InvokeCleanupOnceAsync(service);

        await using var assertScope = provider.CreateAsyncScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var expiredPendingExists = await assertDbContext.Invitations.AnyAsync(x => x.Id == expiredPendingId);
        var activePendingExists = await assertDbContext.Invitations.AnyAsync(x => x.Id == activePendingId);

        Assert.False(expiredPendingExists);
        Assert.True(activePendingExists);
    }

    [Fact]
    public async Task DeleteExpiredInvitationsAsync_LeavesAcceptedInvitationsEvenIfExpired()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));

        await using var provider = services.BuildServiceProvider();

        Guid expiredAcceptedId;

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            (_, _, expiredAcceptedId) = await SeedInvitationsAsync(dbContext);
        }

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger = provider.GetRequiredService<ILogger<InvitationCleanupService>>();
        var service = new InvitationCleanupService(scopeFactory, logger, Options.Create(new InvitationOptions()));

        await InvokeCleanupOnceAsync(service);

        await using var assertScope = provider.CreateAsyncScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var acceptedExpiredExists = await assertDbContext.Invitations.AnyAsync(x => x.Id == expiredAcceptedId);

        Assert.True(acceptedExpiredExists);
    }

    private static async Task<(Guid expiredPendingId, Guid activePendingId, Guid expiredAcceptedId)> SeedInvitationsAsync(
        ApplicationDbContext dbContext)
    {
        var ownerId = Guid.NewGuid();
        var accepterId = Guid.NewGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.test";
        var accepterEmail = $"accepter-{Guid.NewGuid():N}@example.test";

        dbContext.Users.Add(new ApplicationUser
        {
            Id = ownerId,
            UserName = ownerEmail,
            Email = ownerEmail,
            NormalizedUserName = ownerEmail.ToUpperInvariant(),
            NormalizedEmail = ownerEmail.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.Users.Add(new ApplicationUser
        {
            Id = accepterId,
            UserName = accepterEmail,
            Email = accepterEmail,
            NormalizedUserName = accepterEmail.ToUpperInvariant(),
            NormalizedEmail = accepterEmail.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Project-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var expiredPending = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Email = $"pending-expired-{Guid.NewGuid():N}@example.test",
            Token = Guid.NewGuid().ToString("N"),
            InvitedBy = ownerId,
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            AcceptedAt = null,
            AcceptedBy = null
        };

        var activePending = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Email = $"pending-active-{Guid.NewGuid():N}@example.test",
            Token = Guid.NewGuid().ToString("N"),
            InvitedBy = ownerId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(2),
            AcceptedAt = null,
            AcceptedBy = null
        };

        var expiredAccepted = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Email = $"accepted-expired-{Guid.NewGuid():N}@example.test",
            Token = Guid.NewGuid().ToString("N"),
            InvitedBy = ownerId,
            CreatedAt = DateTime.UtcNow.AddDays(-12),
            ExpiresAt = DateTime.UtcNow.AddDays(-2),
            AcceptedAt = DateTime.UtcNow.AddDays(-5),
            AcceptedBy = accepterId
        };

        dbContext.Projects.Add(project);
        dbContext.Invitations.AddRange(expiredPending, activePending, expiredAccepted);
        await dbContext.SaveChangesAsync();

        return (expiredPending.Id, activePending.Id, expiredAccepted.Id);
    }

    private static async Task InvokeCleanupOnceAsync(InvitationCleanupService service)
    {
        var cleanupMethod = typeof(InvitationCleanupService).GetMethod(
            "DeleteExpiredInvitationsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(cleanupMethod);

        var task = cleanupMethod!.Invoke(service, [CancellationToken.None]) as Task;
        Assert.NotNull(task);
        await task!;
    }
}