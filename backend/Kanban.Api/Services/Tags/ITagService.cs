using Kanban.Api.Models;

namespace Kanban.Api.Services.Tags;

public interface ITagService
{
    Task<Tag> CreateAsync(Guid projectId, Guid userId, CreateTagDto data);
    Task<Tag> GetByIdAsync(Guid tagId, Guid userId);
    Task<IReadOnlyList<Tag>> ListAsync(Guid projectId, Guid userId);
    Task<Tag> UpdateAsync(Guid tagId, Guid userId, UpdateTagDto data);
    Task DeleteAsync(Guid tagId, Guid userId);
}
