using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Subscriptions;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _dbContext;

    public SubscriptionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Subscription> SubscribeAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        EnsureNonEmptyEntityId(entityId);

        var projectId = await ResolveProjectIdAsync(entityType, entityId);
        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var existing = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EntityType == entityType && x.EntityId == entityId);

        if (existing is not null)
        {
            throw new ConflictException("Subscription already exists.");
        }

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        return subscription;
    }

    public async Task UnsubscribeAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        EnsureNonEmptyEntityId(entityId);

        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EntityType == entityType && x.EntityId == entityId);

        if (subscription is null)
        {
            return;
        }

        _dbContext.Subscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UnsubscribeByIdAsync(Guid userId, Guid subscriptionId)
    {
        EnsureNonEmptySubscriptionId(subscriptionId);

        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(x => x.Id == subscriptionId && x.UserId == userId);

        if (subscription is null)
        {
            throw new KeyNotFoundException("Subscription not found.");
        }

        _dbContext.Subscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(EntityType entityType, Guid entityId)
    {
        EnsureNonEmptyEntityId(entityId);

        return await _dbContext.Subscriptions
            .AsNoTracking()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => x.UserId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        EnsureNonEmptyEntityId(entityId);

        var projectId = await ResolveProjectIdAsync(entityType, entityId);
        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return await GetSubscriberIdsAsync(entityType, entityId);
    }

    public async Task<bool> IsSubscribedAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        EnsureNonEmptyEntityId(entityId);

        return await _dbContext.Subscriptions
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.EntityType == entityType && x.EntityId == entityId);
    }

    private async Task<Guid> ResolveProjectIdAsync(EntityType entityType, Guid entityId)
    {
        Guid? projectId = entityType switch
        {
            EntityType.Project => await _dbContext.Projects
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(),
            EntityType.Column => await _dbContext.Columns
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => (Guid?)x.Board.ProjectId)
                .FirstOrDefaultAsync(),
            EntityType.Card => await _dbContext.Cards
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => (Guid?)x.Column.Board.ProjectId)
                .FirstOrDefaultAsync(),
            _ => throw new InvalidOperationException($"Unsupported entity type: {entityType}.")
        };

        if (projectId is null)
        {
            throw new KeyNotFoundException($"{entityType} not found.");
        }

        return projectId.Value;
    }

    private async Task<bool> CheckProjectAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole)
    {
        var role = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .Select(x => (ProjectRole?)x.Role)
            .FirstOrDefaultAsync();

        if (role is null)
        {
            return false;
        }

        return HasRequiredRole(role.Value, minimumRole);
    }

    private static bool HasRequiredRole(ProjectRole actualRole, ProjectRole minimumRole)
    {
        return actualRole <= minimumRole;
    }

    private static void EnsureNonEmptyEntityId(Guid entityId)
    {
        if (entityId == Guid.Empty)
        {
            throw new InvalidOperationException("Entity ID is required.");
        }
    }

    private static void EnsureNonEmptySubscriptionId(Guid subscriptionId)
    {
        if (subscriptionId == Guid.Empty)
        {
            throw new InvalidOperationException("Subscription ID is required.");
        }
    }
}
