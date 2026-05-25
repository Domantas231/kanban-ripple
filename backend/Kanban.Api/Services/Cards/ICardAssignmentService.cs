namespace Kanban.Api.Services.Cards;

public interface ICardAssignmentService
{
    Task AssignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default);
    Task UnassignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default);
    Task AssignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default);
    Task UnassignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default);
}
