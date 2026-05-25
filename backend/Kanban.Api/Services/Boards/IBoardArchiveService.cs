namespace Kanban.Api.Services.Boards;

public interface IBoardArchiveService
{
    Task ArchiveAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default);
    Task PurgeAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default);
}
