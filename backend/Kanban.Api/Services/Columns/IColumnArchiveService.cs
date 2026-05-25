namespace Kanban.Api.Services.Columns;

public interface IColumnArchiveService
{
    Task ArchiveAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default);
    Task PurgeAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default);
}
