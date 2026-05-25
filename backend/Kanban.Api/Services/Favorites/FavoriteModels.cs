using Kanban.Api.Models;

namespace Kanban.Api.Services.Favorites;

public sealed record ToggleFavoriteRequest(EntityType EntityType, Guid EntityId);

public sealed record FavoriteDto(Guid Id, EntityType EntityType, Guid EntityId, DateTime CreatedAt);
