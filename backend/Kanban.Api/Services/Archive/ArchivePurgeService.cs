using Kanban.Api.Data;
using Kanban.Api.Services.Google;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Archive;

public sealed class ArchivePurgeService : IArchivePurgeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IGoogleDriveLinkService? _googleDriveLinkService;
    private readonly ILogger<ArchivePurgeService> _logger;

    public ArchivePurgeService(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<ArchivePurgeService> logger,
        IGoogleDriveLinkService? googleDriveLinkService = null)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;
        _googleDriveLinkService = googleDriveLinkService;
    }

    public async Task PurgeProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var cardIdsQuery =
            from card in _dbContext.Cards.IgnoreQueryFilters()
            join column in _dbContext.Columns.IgnoreQueryFilters() on card.ColumnId equals column.Id
            join board in _dbContext.Boards.IgnoreQueryFilters() on column.BoardId equals board.Id
            where board.ProjectId == projectId
            select card.Id;

        await RevokeDriveLinksAsync(cardIdsQuery, cancellationToken);

        var storageKeys = await (
            from attachment in _dbContext.Attachments.IgnoreQueryFilters()
            join card in _dbContext.Cards.IgnoreQueryFilters() on attachment.CardId equals card.Id
            join column in _dbContext.Columns.IgnoreQueryFilters() on card.ColumnId equals column.Id
            join board in _dbContext.Boards.IgnoreQueryFilters() on column.BoardId equals board.Id
            where board.ProjectId == projectId && !string.IsNullOrWhiteSpace(attachment.StorageKey)
            select attachment.StorageKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        await DeleteStorageKeysAsync(storageKeys, cancellationToken);

        await _dbContext.Projects
            .IgnoreQueryFilters()
            .Where(x => x.Id == projectId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task PurgeBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var cardIdsQuery =
            from card in _dbContext.Cards.IgnoreQueryFilters()
            join column in _dbContext.Columns.IgnoreQueryFilters() on card.ColumnId equals column.Id
            where column.BoardId == boardId
            select card.Id;

        await RevokeDriveLinksAsync(cardIdsQuery, cancellationToken);

        var storageKeys = await (
            from attachment in _dbContext.Attachments.IgnoreQueryFilters()
            join card in _dbContext.Cards.IgnoreQueryFilters() on attachment.CardId equals card.Id
            join column in _dbContext.Columns.IgnoreQueryFilters() on card.ColumnId equals column.Id
            where column.BoardId == boardId && !string.IsNullOrWhiteSpace(attachment.StorageKey)
            select attachment.StorageKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        await DeleteStorageKeysAsync(storageKeys, cancellationToken);

        await _dbContext.Boards
            .IgnoreQueryFilters()
            .Where(x => x.Id == boardId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task PurgeColumnAsync(Guid columnId, CancellationToken cancellationToken = default)
    {
        var cardIdsQuery = _dbContext.Cards
            .IgnoreQueryFilters()
            .Where(c => c.ColumnId == columnId)
            .Select(c => c.Id);

        await RevokeDriveLinksAsync(cardIdsQuery, cancellationToken);

        var storageKeys = await (
            from attachment in _dbContext.Attachments.IgnoreQueryFilters()
            join card in _dbContext.Cards.IgnoreQueryFilters() on attachment.CardId equals card.Id
            where card.ColumnId == columnId && !string.IsNullOrWhiteSpace(attachment.StorageKey)
            select attachment.StorageKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        await DeleteStorageKeysAsync(storageKeys, cancellationToken);

        await _dbContext.Columns
            .IgnoreQueryFilters()
            .Where(x => x.Id == columnId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task PurgeCardAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        await RevokeDriveLinksAsync([cardId], cancellationToken);

        var storageKeys = await _dbContext.Attachments
            .IgnoreQueryFilters()
            .Where(x => x.CardId == cardId && !string.IsNullOrWhiteSpace(x.StorageKey))
            .Select(x => x.StorageKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        await DeleteStorageKeysAsync(storageKeys, cancellationToken);

        await _dbContext.Cards
            .IgnoreQueryFilters()
            .Where(x => x.Id == cardId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task RevokeDriveLinksAsync(IQueryable<Guid> cardIdQuery, CancellationToken cancellationToken)
    {
        if (_googleDriveLinkService is null)
        {
            return;
        }

        var cardIds = await cardIdQuery.Distinct().ToListAsync(cancellationToken);
        await RevokeDriveLinksAsync(cardIds, cancellationToken);
    }

    private async Task RevokeDriveLinksAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken cancellationToken)
    {
        if (_googleDriveLinkService is null || cardIds.Count == 0)
        {
            return;
        }

        try
        {
            await _googleDriveLinkService.RevokePermissionsForCardsAsync(cardIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Archive purge failed revoking Google Drive permissions for {CardCount} card(s).", cardIds.Count);
        }
    }

    private async Task DeleteStorageKeysAsync(IReadOnlyCollection<string> storageKeys, CancellationToken cancellationToken)
    {
        foreach (var storageKey in storageKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _fileStorageService.DeleteAsync(storageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Archive purge failed deleting storage key {StorageKey}.", storageKey);
            }
        }
    }
}
