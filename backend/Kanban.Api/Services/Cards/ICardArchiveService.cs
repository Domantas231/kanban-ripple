namespace Kanban.Api.Services.Cards;

public interface ICardArchiveService
{
    Task ArchiveAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task PurgeAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
}
