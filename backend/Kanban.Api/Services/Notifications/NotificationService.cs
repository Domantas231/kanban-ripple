using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 20;

    private readonly ApplicationDbContext _dbContext;

    public NotificationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Notification> CreateAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null,
        Guid? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Notification title is required.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException("Notification message is required.");
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Message = message.Trim(),
            EntityType = string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim(),
            EntityId = entityId,
            CreatedBy = createdBy,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        return notification;
    }

    public async Task<PaginatedResponse<Notification>> ListAsync(Guid userId, int page, int pageSize)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        var query = _dbContext.Notifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync();

        return new PaginatedResponse<Notification>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(notification => notification.Id == notificationId && notification.UserId == userId);

        if (notification is null)
        {
            throw new KeyNotFoundException("Notification not found.");
        }

        if (notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var unreadQuery = _dbContext.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead);

        if (_dbContext.Database.IsRelational())
        {
            await unreadQuery.ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true));
            return;
        }

        var unreadItems = await unreadQuery.ToListAsync();
        if (unreadItems.Count == 0)
        {
            return;
        }

        foreach (var notification in unreadItems)
        {
            notification.IsRead = true;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid notificationId, Guid userId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(notification => notification.Id == notificationId && notification.UserId == userId);

        if (notification is null)
        {
            throw new KeyNotFoundException("Notification not found.");
        }

        _dbContext.Notifications.Remove(notification);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _dbContext.Notifications
            .CountAsync(notification => notification.UserId == userId && !notification.IsRead);
    }
}
