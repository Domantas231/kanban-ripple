using Kanban.Api.Models;
using Kanban.Api.Services.Projects;

namespace Kanban.Api.Services.Notifications;

public interface INotificationService
{
    Task<Notification> CreateAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? entityType = null,
        Guid? entityId = null,
        Guid? createdBy = null);

    Task<PaginatedResponse<Notification>> ListAsync(Guid userId, int page, int pageSize);
    Task MarkAsReadAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
    Task DeleteAsync(Guid notificationId, Guid userId);
    Task<int> GetUnreadCountAsync(Guid userId);
}
