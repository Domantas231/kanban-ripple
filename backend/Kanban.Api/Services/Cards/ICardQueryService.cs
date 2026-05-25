using Kanban.Api.Models;
using Kanban.Api.Services.Projects;

namespace Kanban.Api.Services.Cards;

public interface ICardQueryService
{
    Task<PaginatedResponse<Card>> ListByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<Card>> SearchAsync(Guid projectId, Guid userId, string query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<List<Card>> FilterAsync(Guid boardId, Guid userId, FilterCriteria filters, CancellationToken cancellationToken = default);
    Task<Card> GetByIdAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<Card>> ListArchivedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<Card>> ListArchivedByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
