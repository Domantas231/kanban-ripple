namespace Kanban.Api.Services.Archive;

public interface IArchivePurgeService
{
    Task PurgeProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task PurgeBoardAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task PurgeColumnAsync(Guid columnId, CancellationToken cancellationToken = default);
    Task PurgeCardAsync(Guid cardId, CancellationToken cancellationToken = default);
}
