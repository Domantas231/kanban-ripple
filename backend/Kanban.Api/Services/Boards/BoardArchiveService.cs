using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Archive;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Boards;

public sealed class BoardArchiveService : IBoardArchiveService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly INotificationFanout _notificationFanout;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IArchivePurgeService _archivePurgeService;

    public BoardArchiveService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder,
        INotificationFanout notificationFanout,
        IProjectBroadcaster projectBroadcaster,
        IArchivePurgeService archivePurgeService)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
        _notificationFanout = notificationFanout;
        _projectBroadcaster = projectBroadcaster;
        _archivePurgeService = archivePurgeService;
    }

    public async Task ArchiveAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member);

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        board.DeletedAt = now;
        board.UpdatedAt = now;

        var columns = await _dbContext.Columns
            .Where(x => x.BoardId == board.Id)
            .ToListAsync(cancellationToken);

        var columnIds = columns
            .Select(x => x.Id)
            .ToList();

        var cards = await _dbContext.Cards
            .Where(x => columnIds.Contains(x.ColumnId))
            .ToListAsync(cancellationToken);

        foreach (var column in columns)
        {
            column.DeletedAt = now;
            column.UpdatedAt = now;
        }

        foreach (var card in cards)
        {
            card.DeletedAt = now;
            card.UpdatedAt = now;
        }

        var cardIds = cards.Select(x => x.Id).ToList();
        var favorites = await _dbContext.Favorites
            .Where(f =>
                (f.EntityType == EntityType.Board && f.EntityId == boardId) ||
                (f.EntityType == EntityType.Card && cardIds.Contains(f.EntityId)))
            .ToListAsync(cancellationToken);
        _dbContext.Favorites.RemoveRange(favorites);

        var subscriptions = await _dbContext.Subscriptions
            .Where(s =>
                (s.EntityType == EntityType.Board && s.EntityId == boardId) ||
                (s.EntityType == EntityType.Column && columnIds.Contains(s.EntityId)) ||
                (s.EntityType == EntityType.Card && cardIds.Contains(s.EntityId)))
            .ToListAsync(cancellationToken);
        _dbContext.Subscriptions.RemoveRange(subscriptions);

        _activityRecorder.RecordBoard(board.Id, userId, ActivityAction.Archived);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.BoardDeleted(board.ProjectId, board.Id);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Deleted,
            $"Board archived: {board.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} archived board '{board.Name}'.",
            entityType: EntityType.Board,
            entityId: board.Id,
            createdBy: userId,
            (EntityType.Board, board.Id),
            (EntityType.Project, board.ProjectId));
    }

    public async Task RestoreAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member);

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        board.DeletedAt = null;
        board.UpdatedAt = now;

        var columns = await _dbContext.Columns
            .IgnoreQueryFilters()
            .Where(x => x.BoardId == board.Id)
            .ToListAsync(cancellationToken);

        var columnIds = columns
            .Select(x => x.Id)
            .ToList();

        var cards = await _dbContext.Cards
            .IgnoreQueryFilters()
            .Where(x => columnIds.Contains(x.ColumnId))
            .ToListAsync(cancellationToken);

        foreach (var column in columns)
        {
            column.DeletedAt = null;
            column.UpdatedAt = now;
        }

        foreach (var card in cards)
        {
            card.DeletedAt = null;
            card.UpdatedAt = now;
        }

        _activityRecorder.RecordBoard(board.Id, userId, ActivityAction.Restored);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.BoardCreated(board.ProjectId, board);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Created,
            $"Board restored: {board.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} restored board '{board.Name}'.",
            entityType: EntityType.Board,
            entityId: board.Id,
            createdBy: userId,
            (EntityType.Board, board.Id),
            (EntityType.Project, board.ProjectId));
    }

    public async Task PurgeAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }

        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member, cancellationToken);

        if (board.DeletedAt is null)
        {
            throw new BadRequestException("Board must be archived before it can be permanently deleted.");
        }

        await _archivePurgeService.PurgeBoardAsync(boardId, cancellationToken);

        await _projectBroadcaster.BoardDeleted(board.ProjectId, board.Id);
    }
}
