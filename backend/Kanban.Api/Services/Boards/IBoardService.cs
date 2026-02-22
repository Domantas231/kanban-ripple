using Kanban.Api.Models;

namespace Kanban.Api.Services.Boards;

public interface IBoardService
{
    Task<Board> CreateAsync(Guid projectId, Guid userId, string name);
    Task<Board> GetByIdAsync(Guid boardId, Guid userId);
    Task<IReadOnlyList<Board>> ListAsync(Guid projectId, Guid userId);
    Task<Board> UpdateAsync(Guid boardId, Guid userId, UpdateBoardDto data);
    Task ArchiveAsync(Guid boardId, Guid userId);
    Task RestoreAsync(Guid boardId, Guid userId);
}
