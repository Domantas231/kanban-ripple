using Kanban.Api.Models;

namespace Kanban.Api.Services.Boards;

public interface IBoardService
{
    Task<Board> CreateAsync(Guid projectId, Guid userId, string name, CancellationToken cancellationToken = default);
    Task<Board> ImportFromTrelloAsync(Guid projectId, Guid userId, TrelloImportRequest trelloData, CancellationToken cancellationToken = default);
    Task<Board> GetByIdAsync(Guid boardId, Guid userId);
    Task<IReadOnlyList<Board>> ListAsync(Guid projectId, Guid userId);
    Task<IReadOnlyList<Board>> ListArchivedAsync(Guid userId);
    Task<Board> UpdateAsync(Guid boardId, Guid userId, UpdateBoardDto data, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default);
    Task PurgeAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default);
}
