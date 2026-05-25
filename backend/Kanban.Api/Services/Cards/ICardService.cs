using Kanban.Api.Models;
using Kanban.Api.Services.Projects;

namespace Kanban.Api.Services.Cards;

public interface ICardService
{
    Task<PaginatedResponse<Card>> ListByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<Card>> SearchAsync(Guid projectId, Guid userId, string query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<List<Card>> FilterAsync(Guid boardId, Guid userId, FilterCriteria filters, CancellationToken cancellationToken = default);
    Task<Card> CreateAsync(Guid columnId, Guid userId, CreateCardDto data, CancellationToken cancellationToken = default);
    Task<Card> GetByIdAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task<Card> UpdateAsync(Guid cardId, Guid userId, UpdateCardDto data, CancellationToken cancellationToken = default);
    Task<Card> ScheduleAsync(Guid cardId, Guid userId, ScheduleCardDto data, CancellationToken cancellationToken = default);
    Task<Card> MoveAsync(Guid cardId, Guid userId, MoveCardDto data, CancellationToken cancellationToken = default);
    Task AssignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default);
    Task UnassignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default);
    Task AssignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default);
    Task UnassignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default);
    Task<Subtask> CreateSubtaskAsync(Guid cardId, Guid userId, CreateSubtaskDto data, CancellationToken cancellationToken = default);
    Task<Subtask> UpdateSubtaskAsync(Guid subtaskId, Guid userId, UpdateSubtaskDto data, CancellationToken cancellationToken = default);
    Task DeleteSubtaskAsync(Guid subtaskId, Guid userId, CancellationToken cancellationToken = default);
    Task<SubtaskCountsDto> GetSubtaskCountsAsync(Guid cardId, Guid userId);
    Task ArchiveAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task PurgeAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<Card>> ListArchivedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<Card>> ListArchivedByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
