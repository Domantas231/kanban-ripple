using Kanban.Api.Models;

namespace Kanban.Api.Hubs;

public interface IProjectBroadcaster
{
    Task CardCreated(Guid projectId, Card card);
    Task CardUpdated(Guid projectId, Card card);
    Task CardDeleted(Guid projectId, Guid cardId);
    Task CardMoved(Guid projectId, Card card);

    Task ColumnCreated(Guid projectId, Column column);
    Task ColumnUpdated(Guid projectId, Column column);
    Task ColumnDeleted(Guid projectId, Guid columnId);

    Task BoardCreated(Guid projectId, Board board);
    Task BoardUpdated(Guid projectId, Board board);
    Task BoardDeleted(Guid projectId, Guid boardId);

    Task CommentCreated(Guid projectId, Comment comment);
    Task CommentUpdated(Guid projectId, Comment comment);
    Task CommentDeleted(Guid projectId, Guid commentId);

    Task NotificationReceived(Guid userId, Notification notification);

    Task PlannerBlockChanged(Guid projectId, Guid userId);

    Task UserJoined(Guid projectId, Guid userId);
    Task UserLeft(Guid projectId, Guid userId);
}
