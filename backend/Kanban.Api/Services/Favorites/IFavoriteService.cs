using Kanban.Api.Models;

namespace Kanban.Api.Services.Favorites;

public interface IFavoriteService
{
    Task<IReadOnlyList<Favorite>> ListAsync(Guid userId);
    Task<Favorite> ToggleAsync(Guid userId, EntityType entityType, Guid entityId);
    Task<bool> IsFavoritedAsync(Guid userId, EntityType entityType, Guid entityId);
}
