using Kanban.Api.Models;

namespace Kanban.Api.Services.Columns;

public interface IColumnService
{
    Task<Column> CreateAsync(Guid boardId, Guid userId, string name, CancellationToken cancellationToken = default);
    Task<Column> GetByIdAsync(Guid columnId, Guid userId);
    Task<IReadOnlyList<Column>> ListAsync(Guid boardId, Guid userId);
    Task<Column> UpdateAsync(Guid columnId, Guid userId, UpdateColumnDto data, CancellationToken cancellationToken = default);
    Task<Column> ReorderAsync(Guid columnId, Guid userId, ReorderColumnDto data, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default);
    Task PurgeAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Column>> ListArchivedByBoardAsync(Guid boardId, Guid userId);
}
