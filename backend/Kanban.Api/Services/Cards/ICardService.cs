using Kanban.Api.Models;

namespace Kanban.Api.Services.Cards;

public interface ICardService
{
    Task<Card> CreateAsync(Guid columnId, Guid userId, CreateCardDto data);
    Task<Card> GetByIdAsync(Guid cardId, Guid userId);
    Task<Card> UpdateAsync(Guid cardId, Guid userId, UpdateCardDto data);
    Task ArchiveAsync(Guid cardId, Guid userId);
    Task RestoreAsync(Guid cardId, Guid userId);
}
