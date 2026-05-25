using Kanban.Api.Models;
using Kanban.Api.Services.Notifications;

namespace Kanban.Api.Tests.TestDoubles;

public sealed class NoOpNotificationFanout : INotificationFanout
{
    public Task FanOutAsync(
        Guid actorUserId,
        NotificationType notificationType,
        string title,
        string message,
        EntityType? entityType,
        Guid? entityId,
        Guid? createdBy,
        params (EntityType EntityType, Guid EntityId)[] subscriptionScopes)
        => Task.CompletedTask;

    public Task<string> GetActorLabelAsync(Guid actorUserId) => Task.FromResult("Test user");
}
