using Kanban.Api.Hubs;
using Kanban.Api.Models;

namespace Kanban.Api.Tests.TestDoubles;

public sealed class NoOpProjectBroadcaster : IProjectBroadcaster
{
    public Task CardCreated(Guid projectId, Card card) => Task.CompletedTask;
    public Task CardUpdated(Guid projectId, Card card) => Task.CompletedTask;
    public Task CardDeleted(Guid projectId, Guid cardId) => Task.CompletedTask;
    public Task CardMoved(Guid projectId, Card card) => Task.CompletedTask;

    public Task ColumnCreated(Guid projectId, Column column) => Task.CompletedTask;
    public Task ColumnUpdated(Guid projectId, Column column) => Task.CompletedTask;
    public Task ColumnDeleted(Guid projectId, Guid columnId) => Task.CompletedTask;

    public Task BoardCreated(Guid projectId, Board board) => Task.CompletedTask;
    public Task BoardUpdated(Guid projectId, Board board) => Task.CompletedTask;
    public Task BoardDeleted(Guid projectId, Guid boardId) => Task.CompletedTask;

    public Task CommentCreated(Guid projectId, Comment comment) => Task.CompletedTask;
    public Task CommentUpdated(Guid projectId, Comment comment) => Task.CompletedTask;
    public Task CommentDeleted(Guid projectId, Guid commentId) => Task.CompletedTask;

    public Task TagCreated(Guid projectId, Tag tag) => Task.CompletedTask;
    public Task TagUpdated(Guid projectId, Tag tag) => Task.CompletedTask;
    public Task TagDeleted(Guid projectId, Guid tagId) => Task.CompletedTask;

    public Task NotificationReceived(Guid userId, Notification notification) => Task.CompletedTask;

    public Task PlannerBlockChanged(Guid projectId, Guid userId) => Task.CompletedTask;

    public Task UserJoined(Guid projectId, Guid userId) => Task.CompletedTask;
    public Task UserLeft(Guid projectId, Guid userId) => Task.CompletedTask;
}
