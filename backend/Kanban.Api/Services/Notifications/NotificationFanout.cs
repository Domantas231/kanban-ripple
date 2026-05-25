using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Notifications;

public sealed class NotificationFanout : INotificationFanout
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ISubscriptionService _subscriptionService;

    public NotificationFanout(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ISubscriptionService subscriptionService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _subscriptionService = subscriptionService;
    }

    public async Task FanOutAsync(
        Guid actorUserId,
        NotificationType notificationType,
        string title,
        string message,
        EntityType? entityType,
        Guid? entityId,
        Guid? createdBy,
        params (EntityType EntityType, Guid EntityId)[] subscriptionScopes)
    {
        var subscriberIds = new HashSet<Guid>();

        foreach (var (scopeType, scopeId) in subscriptionScopes)
        {
            if (scopeId == Guid.Empty)
            {
                continue;
            }

            var scopeSubscriberIds = await _subscriptionService.GetSubscriberIdsAsync(scopeType, scopeId);
            foreach (var subscriberId in scopeSubscriberIds)
            {
                subscriberIds.Add(subscriberId);
            }
        }

        if (subscriberIds.Count == 0)
        {
            return;
        }

        foreach (var subscriberId in subscriberIds.Where(x => x != actorUserId))
        {
            await _notificationService.CreateAsync(
                subscriberId,
                notificationType,
                title,
                message,
                entityType: entityType,
                entityId: entityId,
                createdBy: createdBy);
        }
    }

    public async Task<string> GetActorLabelAsync(Guid actorUserId)
    {
        var actorDisplay = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == actorUserId)
            .Select(x => x.UserName ?? x.Email)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(actorDisplay) ? "A user" : actorDisplay;
    }
}
