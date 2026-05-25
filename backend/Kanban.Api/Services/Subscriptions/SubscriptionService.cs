using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Subscriptions;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;

    public SubscriptionService(ApplicationDbContext dbContext, IProjectAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
    }

    public async Task<Subscription> SubscribeAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        EnsureNonEmptyEntityId(entityId);

        var projectId = await ResolveProjectIdAsync(entityType, entityId);
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

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
            throw new NotFoundException("Subscription not found.");
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
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        return await GetSubscriberIdsAsync(entityType, entityId);
    }

    public async Task<bool> IsSubscribedAsync(Guid userId, EntityType entityType, Guid entityId)
    {
        EnsureNonEmptyEntityId(entityId);

        return await _dbContext.Subscriptions
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.EntityType == entityType && x.EntityId == entityId);
    }

    public async Task<IReadOnlyList<MySubscriptionDto>> GetMySubscriptionsAsync(Guid userId)
    {
        var subscriptions = await _dbContext.Subscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var results = new List<MySubscriptionDto>(subscriptions.Count);

        var cardIds = subscriptions.Where(s => s.EntityType == EntityType.Card).Select(s => s.EntityId).ToList();
        var columnIds = subscriptions.Where(s => s.EntityType == EntityType.Column).Select(s => s.EntityId).ToList();
        var boardIds = subscriptions.Where(s => s.EntityType == EntityType.Board).Select(s => s.EntityId).ToList();
        var projectIds = subscriptions.Where(s => s.EntityType == EntityType.Project).Select(s => s.EntityId).ToList();

        var cardLookup = cardIds.Count > 0
            ? (await _dbContext.Cards
                .AsNoTracking()
                .Where(c => cardIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Title, ColumnName = c.Column.Name, BoardId = c.Column.BoardId, BoardName = c.Column.Board.Name, ProjectName = c.Column.Board.Project.Name, ProjectId = c.Column.Board.ProjectId })
                .ToListAsync())
                .ToDictionary(c => c.Id)
            : null;

        var columnLookup = columnIds.Count > 0
            ? (await _dbContext.Columns
                .AsNoTracking()
                .Where(c => columnIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, BoardId = c.BoardId, BoardName = c.Board.Name, ProjectName = c.Board.Project.Name, ProjectId = c.Board.ProjectId })
                .ToListAsync())
                .ToDictionary(c => c.Id)
            : null;

        var boardLookup = boardIds.Count > 0
            ? (await _dbContext.Boards
                .AsNoTracking()
                .Where(b => boardIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Name, ProjectName = b.Project.Name, b.ProjectId })
                .ToListAsync())
                .ToDictionary(b => b.Id)
            : null;

        var projectLookup = projectIds.Count > 0
            ? (await _dbContext.Projects
                .AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync())
                .ToDictionary(p => p.Id)
            : null;

        foreach (var sub in subscriptions)
        {
            var dto = sub.EntityType switch
            {
                EntityType.Card when cardLookup is not null && cardLookup.TryGetValue(sub.EntityId, out var card) =>
                    new MySubscriptionDto(sub.Id, sub.EntityType, sub.EntityId, card.Title, card.ProjectName, card.ProjectId, card.BoardId, card.BoardName, card.ColumnName, sub.CreatedAt),
                EntityType.Column when columnLookup is not null && columnLookup.TryGetValue(sub.EntityId, out var column) =>
                    new MySubscriptionDto(sub.Id, sub.EntityType, sub.EntityId, column.Name, column.ProjectName, column.ProjectId, column.BoardId, column.BoardName, null, sub.CreatedAt),
                EntityType.Board when boardLookup is not null && boardLookup.TryGetValue(sub.EntityId, out var board) =>
                    new MySubscriptionDto(sub.Id, sub.EntityType, sub.EntityId, board.Name, board.ProjectName, board.ProjectId, sub.EntityId, null, null, sub.CreatedAt),
                EntityType.Project when projectLookup is not null && projectLookup.TryGetValue(sub.EntityId, out var project) =>
                    new MySubscriptionDto(sub.Id, sub.EntityType, sub.EntityId, project.Name, project.Name, sub.EntityId, null, null, null, sub.CreatedAt),
                _ => new MySubscriptionDto(sub.Id, sub.EntityType, sub.EntityId, "(deleted)", null, null, null, null, null, sub.CreatedAt),
            };
            results.Add(dto);
        }

        return results;
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
            EntityType.Board => await _dbContext.Boards
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => (Guid?)x.ProjectId)
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
            _ => throw new BadRequestException($"Unsupported entity type: {entityType}.")
        };

        if (projectId is null)
        {
            throw new NotFoundException($"{entityType} not found.");
        }

        return projectId.Value;
    }

    private static void EnsureNonEmptyEntityId(Guid entityId)
    {
        if (entityId == Guid.Empty)
        {
            throw new BadRequestException("Entity ID is required.");
        }
    }

    private static void EnsureNonEmptySubscriptionId(Guid subscriptionId)
    {
        if (subscriptionId == Guid.Empty)
        {
            throw new BadRequestException("Subscription ID is required.");
        }
    }
}
