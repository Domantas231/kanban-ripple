using Kanban.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kanban.Api.Hubs;

public sealed class ProjectBroadcaster(IHubContext<ProjectHub, IProjectClient> hubContext) : IProjectBroadcaster
{
    public Task CardCreated(Guid projectId, Card card)
        => Group(projectId).CardCreated(card);

    public Task CardUpdated(Guid projectId, Card card)
        => Group(projectId).CardUpdated(card);

    public Task CardDeleted(Guid projectId, Guid cardId)
        => Group(projectId).CardDeleted(cardId);

    public Task CardMoved(Guid projectId, Card card)
        => Group(projectId).CardMoved(card);

    public Task ColumnCreated(Guid projectId, Column column)
        => Group(projectId).ColumnCreated(column);

    public Task ColumnUpdated(Guid projectId, Column column)
        => Group(projectId).ColumnUpdated(column);

    public Task ColumnDeleted(Guid projectId, Guid columnId)
        => Group(projectId).ColumnDeleted(columnId);

    public Task BoardCreated(Guid projectId, Board board)
        => Group(projectId).BoardCreated(board);

    public Task BoardUpdated(Guid projectId, Board board)
        => Group(projectId).BoardUpdated(board);

    public Task BoardDeleted(Guid projectId, Guid boardId)
        => Group(projectId).BoardDeleted(boardId);

    public Task CommentCreated(Guid projectId, Comment comment)
        => Group(projectId).CommentCreated(comment);

    public Task CommentUpdated(Guid projectId, Comment comment)
        => Group(projectId).CommentUpdated(comment);

    public Task CommentDeleted(Guid projectId, Guid commentId)
        => Group(projectId).CommentDeleted(commentId);

    public Task NotificationReceived(Guid userId, Notification notification)
        => hubContext.Clients.User(userId.ToString()).NotificationReceived(notification);

    public Task PlannerBlockChanged(Guid projectId, Guid userId)
        => Group(projectId).PlannerBlockChanged(userId);

    public Task UserJoined(Guid projectId, Guid userId)
        => Group(projectId).UserJoined(userId);

    public Task UserLeft(Guid projectId, Guid userId)
        => Group(projectId).UserLeft(userId);

    private IProjectClient Group(Guid projectId)
        => hubContext.Clients.Group(ProjectHub.GetProjectGroupName(projectId));
}
