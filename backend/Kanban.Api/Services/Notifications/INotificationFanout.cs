using Kanban.Api.Models;

namespace Kanban.Api.Services.Notifications;

public interface INotificationFanout
{
    Task FanOutAsync(
        Guid actorUserId,
        NotificationType notificationType,
        string title,
        string message,
        EntityType? entityType,
        Guid? entityId,
        Guid? createdBy,
        params (EntityType EntityType, Guid EntityId)[] subscriptionScopes);

    Task<string> GetActorLabelAsync(Guid actorUserId);
}
