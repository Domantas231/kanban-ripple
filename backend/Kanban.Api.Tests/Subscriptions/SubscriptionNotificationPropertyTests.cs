using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Kanban.Api.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Subscriptions;

public sealed class SubscriptionNotificationPropertyTests
{
    [Fact]
    public async Task Property_69_ModifyingSubscribedCardNotifiesAllSubscribersExceptModifier()
    {
        await using var fixture = await CreateFixtureAsync();

        var actorId = Guid.NewGuid();
        var subscriberOneId = Guid.NewGuid();
        var subscriberTwoId = Guid.NewGuid();

        AddUser(fixture.DbContext, actorId, "actor-69@example.test");
        AddUser(fixture.DbContext, subscriberOneId, "subscriber-69-1@example.test");
        AddUser(fixture.DbContext, subscriberTwoId, "subscriber-69-2@example.test");

        var card = await SeedProjectBoardColumnAndCardAsync(fixture.DbContext, actorId);

        fixture.SubscriptionService.SubscriberIdsToReturn = new[]
        {
            actorId,
            subscriberOneId,
            subscriberTwoId,
            subscriberOneId
        };

        await fixture.CardService.UpdateAsync(
            card.Id,
            actorId,
            new UpdateCardDto("Updated title", "Updated description", card.PlannedDurationHours, card.Version));

        Assert.Single(fixture.SubscriptionService.GetSubscriberCalls);
        var subscriptionQuery = fixture.SubscriptionService.GetSubscriberCalls[0];
        Assert.Equal(EntityType.Card, subscriptionQuery.EntityType);
        Assert.Equal(card.Id, subscriptionQuery.EntityId);

        Assert.Equal(2, fixture.NotificationService.CreateCalls.Count);
        var notifiedUserIds = fixture.NotificationService.CreateCalls.Select(x => x.UserId).ToHashSet();

        Assert.Contains(subscriberOneId, notifiedUserIds);
        Assert.Contains(subscriberTwoId, notifiedUserIds);
        Assert.DoesNotContain(actorId, notifiedUserIds);
    }

    [Fact]
    public async Task Property_70_NotificationIncludesChangeTypeAndModifierIdentity()
    {
        await using var fixture = await CreateFixtureAsync();

        var actorId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();
        const string actorEmail = "modifier-70@example.test";

        AddUser(fixture.DbContext, actorId, actorEmail);
        AddUser(fixture.DbContext, subscriberId, "subscriber-70@example.test");

        var card = await SeedProjectBoardColumnAndCardAsync(fixture.DbContext, actorId);

        fixture.SubscriptionService.SubscriberIdsToReturn = new[] { subscriberId, actorId };

        var updated = await fixture.CardService.UpdateAsync(
            card.Id,
            actorId,
            new UpdateCardDto("Renamed for property 70", "Body", card.PlannedDurationHours, card.Version));

        var notification = Assert.Single(fixture.NotificationService.CreateCalls);

        Assert.Equal(subscriberId, notification.UserId);
        Assert.Equal(NotificationType.CardUpdated, notification.Type);
        Assert.Contains("Card updated", notification.Title);
        Assert.Contains(actorEmail, notification.Message);
        Assert.Contains(updated.Title, notification.Message);
        Assert.Equal("card", notification.EntityType);
        Assert.Equal(updated.Id, notification.EntityId);
        Assert.Equal(actorId, notification.CreatedBy);
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"subscription-notification-property-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var notificationService = new RecordingNotificationService();
        var subscriptionService = new RecordingSubscriptionService();
        var cardService = new CardService(dbContext, notificationService, subscriptionService);

        return new TestFixture(dbContext, cardService, notificationService, subscriptionService);
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

        dbContext.SaveChanges();
    }

    private static async Task<Card> SeedProjectBoardColumnAndCardAsync(ApplicationDbContext dbContext, Guid ownerId)
    {
        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Project-{Guid.NewGuid():N}",
            Type = ProjectType.Team,
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var membership = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = ownerId,
            Role = ProjectRole.Owner,
            JoinedAt = now
        };

        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Board",
            Position = 1000,
            CreatedAt = now,
            UpdatedAt = now
        };

        var column = new Column
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "Todo",
            Position = 1000,
            CreatedAt = now,
            UpdatedAt = now
        };

        var card = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = column.Id,
            Title = "Original title",
            Description = "Original description",
            Position = 1000,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerId
        };

        dbContext.Projects.Add(project);
        dbContext.ProjectMembers.Add(membership);
        dbContext.Boards.Add(board);
        dbContext.Columns.Add(column);
        dbContext.Cards.Add(card);

        await dbContext.SaveChangesAsync();
        return card;
    }

    private sealed record TestFixture(
        ApplicationDbContext DbContext,
        CardService CardService,
        RecordingNotificationService NotificationService,
        RecordingSubscriptionService SubscriptionService) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }

    private sealed class RecordingSubscriptionService : ISubscriptionService
    {
        public IReadOnlyList<Guid> SubscriberIdsToReturn { get; set; } = Array.Empty<Guid>();
        public List<(EntityType EntityType, Guid EntityId)> GetSubscriberCalls { get; } = new();

        public Task<Subscription> SubscribeAsync(Guid userId, EntityType entityType, Guid entityId)
        {
            throw new NotSupportedException();
        }

        public Task UnsubscribeAsync(Guid userId, EntityType entityType, Guid entityId)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(EntityType entityType, Guid entityId)
        {
            GetSubscriberCalls.Add((entityType, entityId));
            return Task.FromResult(SubscriberIdsToReturn);
        }

        public Task<bool> IsSubscribedAsync(Guid userId, EntityType entityType, Guid entityId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationCreateCall> CreateCalls { get; } = new();

        public Task<Notification> CreateAsync(
            Guid userId,
            NotificationType type,
            string title,
            string message,
            string? entityType = null,
            Guid? entityId = null,
            Guid? createdBy = null)
        {
            var call = new NotificationCreateCall(userId, type, title, message, entityType, entityId, createdBy);
            CreateCalls.Add(call);

            return Task.FromResult(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                EntityType = entityType,
                EntityId = entityId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<PaginatedResponse<Notification>> ListAsync(Guid userId, int page, int pageSize)
        {
            throw new NotSupportedException();
        }

        public Task MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            throw new NotSupportedException();
        }

        public Task MarkAllAsReadAsync(Guid userId)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid notificationId, Guid userId)
        {
            throw new NotSupportedException();
        }

        public Task<int> GetUnreadCountAsync(Guid userId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record NotificationCreateCall(
        Guid UserId,
        NotificationType Type,
        string Title,
        string Message,
        string? EntityType,
        Guid? EntityId,
        Guid? CreatedBy);
}