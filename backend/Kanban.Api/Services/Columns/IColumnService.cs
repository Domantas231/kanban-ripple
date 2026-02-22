using Kanban.Api.Models;

namespace Kanban.Api.Services.Columns;

public interface IColumnService
{
    Task<Column> CreateAsync(Guid boardId, Guid userId, string name);
    Task<Column> GetByIdAsync(Guid columnId, Guid userId);
    Task<IReadOnlyList<Column>> ListAsync(Guid boardId, Guid userId);
    Task<Column> UpdateAsync(Guid columnId, Guid userId, UpdateColumnDto data);
    Task<Column> ReorderAsync(Guid columnId, Guid userId, ReorderColumnDto data);
    Task ArchiveAsync(Guid columnId, Guid userId);
    Task RestoreAsync(Guid columnId, Guid userId);
}
