using Kanban.Api.Models;

namespace Kanban.Api.Services.Cards;

public interface ICardService
{
    Task<Card> CreateAsync(Guid columnId, Guid userId, CreateCardDto data);
    Task<Card> GetByIdAsync(Guid cardId, Guid userId);
    Task<Card> UpdateAsync(Guid cardId, Guid userId, UpdateCardDto data);
    Task<Card> MoveAsync(Guid cardId, Guid userId, MoveCardDto data);
    Task AssignTagAsync(Guid cardId, Guid tagId, Guid userId);
    Task UnassignTagAsync(Guid cardId, Guid tagId, Guid userId);
    Task AssignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId);
    Task UnassignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId);
    Task ArchiveAsync(Guid cardId, Guid userId);
    Task RestoreAsync(Guid cardId, Guid userId);
}
