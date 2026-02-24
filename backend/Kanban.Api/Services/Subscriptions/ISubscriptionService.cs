using Kanban.Api.Models;

namespace Kanban.Api.Services.Subscriptions;

public interface ISubscriptionService
{
    Task<Subscription> SubscribeAsync(Guid userId, EntityType entityType, Guid entityId);
    Task UnsubscribeAsync(Guid userId, EntityType entityType, Guid entityId);
    Task UnsubscribeByIdAsync(Guid userId, Guid subscriptionId);
    Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(EntityType entityType, Guid entityId);
    Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(Guid userId, EntityType entityType, Guid entityId);
    Task<bool> IsSubscribedAsync(Guid userId, EntityType entityType, Guid entityId);
}
