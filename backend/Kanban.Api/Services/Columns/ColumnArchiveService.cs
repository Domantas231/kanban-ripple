using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Archive;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Columns;

public sealed class ColumnArchiveService : IColumnArchiveService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly INotificationFanout _notificationFanout;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IArchivePurgeService _archivePurgeService;

    public ColumnArchiveService(
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

    public async Task ArchiveAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default)
    {
        var column = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId, cancellationToken);

        if (column is null)
        {
            throw new NotFoundException("Column not found.");
        }
        await _accessGuard.RequireAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        column.DeletedAt = now;
        column.UpdatedAt = now;

        var cards = await _dbContext.Cards
            .Where(x => x.ColumnId == column.Id)
            .ToListAsync(cancellationToken);

        foreach (var card in cards)
        {
            card.DeletedAt = now;
            card.UpdatedAt = now;
        }

        var cardIds = cards.Select(x => x.Id).ToList();
        var subscriptions = await _dbContext.Subscriptions
            .Where(s =>
                (s.EntityType == EntityType.Column && s.EntityId == columnId) ||
                (s.EntityType == EntityType.Card && cardIds.Contains(s.EntityId)))
            .ToListAsync(cancellationToken);
        _dbContext.Subscriptions.RemoveRange(subscriptions);

        _activityRecorder.RecordColumn(column.Id, userId, ActivityAction.Archived);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.ColumnDeleted(column.Board.ProjectId, column.Id);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Deleted,
            $"List archived: {column.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} archived list '{column.Name}'.",
            entityType: EntityType.Column,
            entityId: column.Id,
            createdBy: userId,
            (EntityType.Column, column.Id),
            (EntityType.Board, column.BoardId),
            (EntityType.Project, column.Board.ProjectId));
    }

    public async Task RestoreAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default)
    {
        var column = await _dbContext.Columns
            .IgnoreQueryFilters()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId, cancellationToken);

        if (column is null)
        {
            throw new NotFoundException("Column not found.");
        }
        await _accessGuard.RequireAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        column.DeletedAt = null;
        column.UpdatedAt = now;

        var cards = await _dbContext.Cards
            .IgnoreQueryFilters()
            .Where(x => x.ColumnId == column.Id)
            .ToListAsync(cancellationToken);

        foreach (var card in cards)
        {
            card.DeletedAt = null;
            card.UpdatedAt = now;
        }

        _activityRecorder.RecordColumn(column.Id, userId, ActivityAction.Restored);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.ColumnCreated(column.Board.ProjectId, column);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Created,
            $"List restored: {column.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} restored list '{column.Name}'.",
            entityType: EntityType.Column,
            entityId: column.Id,
            createdBy: userId,
            (EntityType.Column, column.Id),
            (EntityType.Board, column.BoardId),
            (EntityType.Project, column.Board.ProjectId));
    }

    public async Task PurgeAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default)
    {
        var column = await _dbContext.Columns
            .IgnoreQueryFilters()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId, cancellationToken);

        if (column is null)
        {
            throw new NotFoundException("Column not found.");
        }

        await _accessGuard.RequireAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member, cancellationToken);

        if (column.DeletedAt is null)
        {
            throw new BadRequestException("Column must be archived before it can be permanently deleted.");
        }

        await _archivePurgeService.PurgeColumnAsync(columnId, cancellationToken);

        await _projectBroadcaster.ColumnDeleted(column.Board.ProjectId, column.Id);
    }
}
