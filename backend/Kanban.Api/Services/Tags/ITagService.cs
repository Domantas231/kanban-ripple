using Kanban.Api.Models;

namespace Kanban.Api.Services.Tags;

public interface ITagService
{
    Task<Tag> CreateAsync(Guid boardId, Guid userId, CreateTagDto data, CancellationToken cancellationToken = default);
    Task<Tag> GetByIdAsync(Guid tagId, Guid userId);
    Task<IReadOnlyList<Tag>> ListAsync(Guid boardId, Guid userId);
    Task<Tag> UpdateAsync(Guid tagId, Guid userId, UpdateTagDto data, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid tagId, Guid userId, CancellationToken cancellationToken = default);
}
