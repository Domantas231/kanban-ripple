using Kanban.Api.Models;

namespace Kanban.Api.Services.Comments;

public interface ICommentService
{
    Task<List<Comment>> ListByCardAsync(Guid cardId, Guid userId);
    Task<Comment> CreateAsync(Guid cardId, Guid userId, CreateCommentDto data, CancellationToken cancellationToken = default);
    Task<Comment> UpdateAsync(Guid commentId, Guid userId, UpdateCommentDto data, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default);
}
