using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Favorites;

public sealed class FavoriteService : IFavoriteService
{
    private readonly ApplicationDbContext _dbContext;

    public FavoriteService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Favorite>> ListAsync(Guid userId)
    {
        return await _dbContext.Favorites
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<Favorite> ToggleAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        var existing = await _dbContext.Favorites
            .FirstOrDefaultAsync(f =>
                f.UserId == userId &&
                f.EntityType == entityType &&
                f.EntityId == entityId);

        if (existing is not null)
        {
            _dbContext.Favorites.Remove(existing);
            await _dbContext.SaveChangesAsync();
            return existing;
        }

        var favorite = new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Favorites.Add(favorite);

        if (entityType is EntityType.Board or EntityType.Project)
        {
            var alreadySubscribed = await _dbContext.Subscriptions
                .AnyAsync(s => s.UserId == userId && s.EntityType == entityType && s.EntityId == entityId);

            if (!alreadySubscribed)
            {
                _dbContext.Subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EntityType = entityType,
                    EntityId = entityId,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await _dbContext.SaveChangesAsync();
        return favorite;
    }

    public async Task<bool> IsFavoritedAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        return await _dbContext.Favorites
            .AnyAsync(f =>
                f.UserId == userId &&
                f.EntityType == entityType &&
                f.EntityId == entityId);
    }
}
