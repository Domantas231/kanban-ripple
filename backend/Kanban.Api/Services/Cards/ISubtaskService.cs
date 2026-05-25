using Kanban.Api.Models;

namespace Kanban.Api.Services.Cards;

public interface ISubtaskService
{
    Task<Subtask> CreateAsync(Guid cardId, Guid userId, CreateSubtaskDto data, CancellationToken cancellationToken = default);
    Task<Subtask> UpdateAsync(Guid subtaskId, Guid userId, UpdateSubtaskDto data, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid subtaskId, Guid userId, CancellationToken cancellationToken = default);
    Task<SubtaskCountsDto> GetCountsAsync(Guid cardId, Guid userId);
}
