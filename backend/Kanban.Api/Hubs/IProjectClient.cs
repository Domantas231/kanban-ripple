using Kanban.Api.Models;

namespace Kanban.Api.Hubs;

public interface IProjectClient
{
    Task CardCreated(Card card);
    Task CardUpdated(Card card);
    Task CardDeleted(Guid cardId);
    Task CardMoved(Card card);

    Task ColumnCreated(Column column);
    Task ColumnUpdated(Column column);
    Task ColumnDeleted(Guid columnId);

    Task BoardCreated(Board board);
    Task BoardUpdated(Board board);
    Task BoardDeleted(Guid boardId);

    Task CommentCreated(Comment comment);
    Task CommentUpdated(Comment comment);
    Task CommentDeleted(Guid commentId);

    Task NotificationReceived(Notification notification);

    Task PlannerBlockChanged(Guid userId);

    Task UserJoined(Guid userId);
    Task UserLeft(Guid userId);
}
