using Kanban.Api.Models;
using Kanban.Api.Services.Projects;

namespace Kanban.Api.Services.Cards;

public interface ICardService
{
    Task<PaginatedResponse<Card>> ListByBoardAsync(Guid boardId, Guid userId, int page, int pageSize);
    Task<Card> CreateAsync(Guid columnId, Guid userId, CreateCardDto data);
    Task<Card> GetByIdAsync(Guid cardId, Guid userId);
    Task<Card> UpdateAsync(Guid cardId, Guid userId, UpdateCardDto data);
    Task<Card> MoveAsync(Guid cardId, Guid userId, MoveCardDto data);
    Task AssignTagAsync(Guid cardId, Guid tagId, Guid userId);
    Task UnassignTagAsync(Guid cardId, Guid tagId, Guid userId);
    Task AssignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId);
    Task UnassignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId);
    Task<Subtask> CreateSubtaskAsync(Guid cardId, Guid userId, CreateSubtaskDto data);
    Task<Subtask> UpdateSubtaskAsync(Guid subtaskId, Guid userId, UpdateSubtaskDto data);
    Task DeleteSubtaskAsync(Guid subtaskId, Guid userId);
    Task<SubtaskCountsDto> GetSubtaskCountsAsync(Guid cardId, Guid userId);
    Task ArchiveAsync(Guid cardId, Guid userId);
    Task RestoreAsync(Guid cardId, Guid userId);
    Task<PaginatedResponse<Card>> ListArchivedAsync(Guid userId, int page, int pageSize);
}
