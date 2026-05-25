using Kanban.Api.Models;

namespace Kanban.Api.Services.Subscriptions;

public sealed record CreateSubscriptionRequest(
    EntityType EntityType,
    Guid EntityId);

public sealed record MySubscriptionDto(
    Guid Id,
    EntityType EntityType,
    Guid EntityId,
    string EntityName,
    string? ProjectName,
    Guid? ProjectId,
    Guid? BoardId,
    string? BoardName,
    string? ColumnName,
    DateTime CreatedAt);
