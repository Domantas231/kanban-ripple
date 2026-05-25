using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Archive;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Cards;

public sealed class CardArchiveService : ICardArchiveService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly INotificationFanout _notificationFanout;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IArchivePurgeService _archivePurgeService;

    public CardArchiveService(
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

    public async Task ArchiveAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        card.DeletedAt = now;
        card.UpdatedAt = now;

        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Archived);

        var attachments = await _dbContext.Attachments
            .Where(x => x.CardId == card.Id)
            .ToListAsync(cancellationToken);

        var subtasks = await _dbContext.Subtasks
            .Where(x => x.CardId == card.Id)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            attachment.DeletedAt = now;
        }

        foreach (var subtask in subtasks)
        {
            subtask.DeletedAt = now;
            subtask.UpdatedAt = now;
        }

        var favorites = await _dbContext.Favorites
            .Where(f => f.EntityType == EntityType.Card && f.EntityId == cardId)
            .ToListAsync(cancellationToken);
        _dbContext.Favorites.RemoveRange(favorites);

        var subscriptions = await _dbContext.Subscriptions
            .Where(s => s.EntityType == EntityType.Card && s.EntityId == cardId)
            .ToListAsync(cancellationToken);
        _dbContext.Subscriptions.RemoveRange(subscriptions);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.CardDeleted(card.Column.Board.ProjectId, card.Id);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Deleted,
            $"Task archived: {card.Title}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} archived task '{card.Title}'.",
            entityType: EntityType.Card,
            entityId: card.Id,
            createdBy: userId,
            (EntityType.Card, card.Id),
            (EntityType.Column, card.ColumnId),
            (EntityType.Board, card.Column.BoardId),
            (EntityType.Project, card.Column.Board.ProjectId));
    }

    public async Task RestoreAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .IgnoreQueryFilters()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);

        if (card.Column.DeletedAt is not null)
        {
            throw new BadRequestException("Cannot restore a card to an archived column.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        card.DeletedAt = null;
        card.UpdatedAt = now;

        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Restored);

        var attachments = await _dbContext.Attachments
            .IgnoreQueryFilters()
            .Where(x => x.CardId == card.Id)
            .ToListAsync(cancellationToken);

        var subtasks = await _dbContext.Subtasks
            .IgnoreQueryFilters()
            .Where(x => x.CardId == card.Id)
            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            attachment.DeletedAt = null;
        }

        foreach (var subtask in subtasks)
        {
            subtask.DeletedAt = null;
            subtask.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.CardCreated(card.Column.Board.ProjectId, card);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Created,
            $"Task restored: {card.Title}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} restored task '{card.Title}'.",
            entityType: EntityType.Card,
            entityId: card.Id,
            createdBy: userId,
            (EntityType.Card, card.Id),
            (EntityType.Column, card.ColumnId),
            (EntityType.Board, card.Column.BoardId),
            (EntityType.Project, card.Column.Board.ProjectId));
    }

    public async Task PurgeAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .IgnoreQueryFilters()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }

        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member, cancellationToken);

        if (card.DeletedAt is null)
        {
            throw new BadRequestException("Card must be archived before it can be permanently deleted.");
        }

        var projectId = card.Column.Board.ProjectId;

        await _archivePurgeService.PurgeCardAsync(cardId, cancellationToken);

        await _projectBroadcaster.CardDeleted(projectId, cardId);
    }
}
