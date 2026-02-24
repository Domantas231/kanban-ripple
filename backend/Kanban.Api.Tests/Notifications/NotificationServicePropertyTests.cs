using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Notifications;

public class NotificationServicePropertyTests
{
    [Fact]
    public async Task Property_63_AssignmentCreatesNotificationWithCardTitleAndAssignerInfo()
    {
        await using var fixture = await CreateFixtureAsync();
        var assigneeId = Guid.NewGuid();
        var assignerId = Guid.NewGuid();
        AddUser(fixture.DbContext, assigneeId, "assignee@example.test");
        AddUser(fixture.DbContext, assignerId, "assigner@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var cardId = Guid.NewGuid();
        var cardTitle = "Implement notifications";
        var assignerDisplay = "assigner@example.test";

        var created = await fixture.NotificationService.CreateAsync(
            assigneeId,
            NotificationType.CardAssigned,
            $"Assigned: {cardTitle}",
            $"{assignerDisplay} assigned you to card '{cardTitle}'.",
            entityType: "card",
            entityId: cardId,
            createdBy: assignerId);

        var persisted = await fixture.DbContext.Notifications.SingleAsync(x => x.Id == created.Id);

        Assert.Equal(NotificationType.CardAssigned, persisted.Type);
        Assert.Equal(assigneeId, persisted.UserId);
        Assert.Equal(assignerId, persisted.CreatedBy);
        Assert.Equal("card", persisted.EntityType);
        Assert.Equal(cardId, persisted.EntityId);
        Assert.Contains(cardTitle, persisted.Title);
        Assert.Contains(cardTitle, persisted.Message);
        Assert.Contains(assignerDisplay, persisted.Message);
        Assert.False(persisted.IsRead);
    }

    [Fact]
    public async Task Property_64_MarkAsReadSetsIsReadToTrue()
    {
        await using var fixture = await CreateFixtureAsync();
        var userId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, "mark-read@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var notification = await fixture.NotificationService.CreateAsync(
            userId,
            NotificationType.CardUpdated,
            "Card updated",
            "Card details were updated.");

        await fixture.NotificationService.MarkAsReadAsync(notification.Id, userId);

        var persisted = await fixture.DbContext.Notifications.SingleAsync(x => x.Id == notification.Id);
        Assert.True(persisted.IsRead);
    }

    [Fact]
    public async Task Property_65_UnreadCountMatchesActualUnreadNotifications()
    {
        await using var fixture = await CreateFixtureAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, "count-user@example.test");
        AddUser(fixture.DbContext, otherUserId, "count-other@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var readOne = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardCreated, "Created A", "A");
        var unreadOne = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardUpdated, "Updated B", "B");
        var unreadTwo = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardMoved, "Moved C", "C");
        _ = await fixture.NotificationService.CreateAsync(otherUserId, NotificationType.CardDeleted, "Other", "Other");

        await fixture.NotificationService.MarkAsReadAsync(readOne.Id, userId);

        var unreadCount = await fixture.NotificationService.GetUnreadCountAsync(userId);
        var expectedUnreadCount = await fixture.DbContext.Notifications
            .CountAsync(x => x.UserId == userId && !x.IsRead);

        Assert.Equal(expectedUnreadCount, unreadCount);
        Assert.Equal(2, unreadCount);

        var unreadIds = await fixture.DbContext.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Contains(unreadOne.Id, unreadIds);
        Assert.Contains(unreadTwo.Id, unreadIds);
    }

    [Fact]
    public async Task Property_71_ListAsyncReturnsAllUserNotificationsForPage()
    {
        await using var fixture = await CreateFixtureAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, "list-user@example.test");
        AddUser(fixture.DbContext, otherUserId, "list-other@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var createdIds = new List<Guid>();
        for (var index = 0; index < 12; index++)
        {
            var notification = await fixture.NotificationService.CreateAsync(
                userId,
                NotificationType.CardUpdated,
                $"Title {index}",
                $"Message {index}");
            createdIds.Add(notification.Id);
        }

        _ = await fixture.NotificationService.CreateAsync(otherUserId, NotificationType.CardUpdated, "Other", "Other");

        var listed = await fixture.NotificationService.ListAsync(userId, page: 1, pageSize: 20);

        Assert.Equal(12, listed.TotalCount);
        Assert.Equal(12, listed.Items.Count);
        Assert.Equal(createdIds.OrderBy(x => x), listed.Items.Select(x => x.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task Property_72_ListAsyncReturnsNotificationsOrderedNewestFirst()
    {
        await using var fixture = await CreateFixtureAsync();
        var userId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, "ordering@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var oldest = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.CardCreated,
            Title = "Oldest",
            Message = "Oldest message",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsRead = false
        };
        var middle = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.CardUpdated,
            Title = "Middle",
            Message = "Middle message",
            CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            IsRead = false
        };
        var newest = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.CardMoved,
            Title = "Newest",
            Message = "Newest message",
            CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            IsRead = false
        };

        fixture.DbContext.Notifications.AddRange(oldest, middle, newest);
        await fixture.DbContext.SaveChangesAsync();

        var listed = await fixture.NotificationService.ListAsync(userId, page: 1, pageSize: 20);
        var ids = listed.Items.Select(x => x.Id).ToArray();

        Assert.Equal(3, ids.Length);
        Assert.Equal(newest.Id, ids[0]);
        Assert.Equal(middle.Id, ids[1]);
        Assert.Equal(oldest.Id, ids[2]);
    }

    [Fact]
    public async Task Property_73_ReadUnreadStatusIsMaintainedAcrossOperations()
    {
        await using var fixture = await CreateFixtureAsync();
        var userId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, "status@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var firstUnread = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardCreated, "A", "A");
        var secondUnread = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardUpdated, "B", "B");
        var alreadyRead = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardMoved, "C", "C");
        await fixture.NotificationService.MarkAsReadAsync(alreadyRead.Id, userId);

        await fixture.NotificationService.MarkAsReadAsync(firstUnread.Id, userId);

        var listed = await fixture.NotificationService.ListAsync(userId, page: 1, pageSize: 20);
        var byId = listed.Items.ToDictionary(x => x.Id);

        Assert.True(byId[firstUnread.Id].IsRead);
        Assert.False(byId[secondUnread.Id].IsRead);
        Assert.True(byId[alreadyRead.Id].IsRead);
    }

    [Fact]
    public async Task Property_74_MarkAllAsReadUpdatesAllUnreadNotifications()
    {
        await using var fixture = await CreateFixtureAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, "mark-all@example.test");
        AddUser(fixture.DbContext, otherUserId, "mark-all-other@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var userUnreadOne = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardCreated, "A", "A");
        _ = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardUpdated, "B", "B");
        var userReadOne = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardMoved, "C", "C");
        var otherUnread = await fixture.NotificationService.CreateAsync(otherUserId, NotificationType.CardDeleted, "D", "D");

        await fixture.NotificationService.MarkAsReadAsync(userReadOne.Id, userId);
        await fixture.NotificationService.MarkAllAsReadAsync(userId);

        var userNotifications = await fixture.DbContext.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();

        Assert.NotEmpty(userNotifications);
        Assert.All(userNotifications, x => Assert.True(x.IsRead));

        var stillUnreadForOtherUser = await fixture.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(x => x.Id == otherUnread.Id);

        Assert.False(stillUnreadForOtherUser.IsRead);

        var unreadCount = await fixture.NotificationService.GetUnreadCountAsync(userId);
        Assert.Equal(0, unreadCount);

        var firstUserUnread = await fixture.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(x => x.Id == userUnreadOne.Id);
        Assert.True(firstUserUnread.IsRead);
    }

    [Fact]
    public async Task Property_75_DeleteRemovesNotificationFromList()
    {
        await using var fixture = await CreateFixtureAsync();
        var userId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, "delete@example.test");
        await fixture.DbContext.SaveChangesAsync();

        var keepOne = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardCreated, "Keep one", "Keep one");
        var toDelete = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardUpdated, "Delete me", "Delete me");
        var keepTwo = await fixture.NotificationService.CreateAsync(userId, NotificationType.CardMoved, "Keep two", "Keep two");

        await fixture.NotificationService.DeleteAsync(toDelete.Id, userId);

        var listed = await fixture.NotificationService.ListAsync(userId, page: 1, pageSize: 20);
        var ids = listed.Items.Select(x => x.Id).ToHashSet();

        Assert.Equal(2, listed.TotalCount);
        Assert.Equal(2, listed.Items.Count);
        Assert.Contains(keepOne.Id, ids);
        Assert.Contains(keepTwo.Id, ids);
        Assert.DoesNotContain(toDelete.Id, ids);
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var notificationService = new NotificationService(dbContext);
        return new TestFixture(dbContext, notificationService, connection);
    }

    private static void AddUser(ApplicationDbContext dbContext, Guid userId, string email)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public TestFixture(ApplicationDbContext dbContext, NotificationService notificationService, SqliteConnection connection)
        {
            DbContext = dbContext;
            NotificationService = notificationService;
            Connection = connection;
        }

        public ApplicationDbContext DbContext { get; }
        public NotificationService NotificationService { get; }
        private SqliteConnection Connection { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
