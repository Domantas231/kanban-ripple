using System.Text.Json.Serialization;
using Kanban.Api.Models;

namespace Kanban.Api.Services.Subscriptions;

public sealed record CreateSubscriptionRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] EntityType EntityType,
    Guid EntityId);
