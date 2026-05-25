using System.Data;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kanban.Api.Services.Cards;

public sealed class CardService : ICardService
{
    private const int PositionGap = Positioning.Gap;

    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly INotificationFanout _notificationFanout;
    private readonly IActivityRecorder _activityRecorder;
    private readonly ICardQueryService _queryService;
    private readonly ISubtaskService _subtaskService;
    private readonly ICardAssignmentService _assignmentService;
    private readonly ICardArchiveService _archiveService;
    private readonly ILogger<CardService> _logger;

    public CardService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder,
        ICardQueryService queryService,
        ISubtaskService subtaskService,
        ICardAssignmentService assignmentService,
        ICardArchiveService archiveService,
        INotificationFanout notificationFanout,
        IProjectBroadcaster projectBroadcaster,
        ILogger<CardService> logger)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
        _queryService = queryService;
        _subtaskService = subtaskService;
        _assignmentService = assignmentService;
        _archiveService = archiveService;
        _notificationFanout = notificationFanout;
        _projectBroadcaster = projectBroadcaster;
        _logger = logger;
    }

    public Task<PaginatedResponse<Card>> ListByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        _queryService.ListByBoardAsync(boardId, userId, page, pageSize, cancellationToken);

    public Task<PaginatedResponse<Card>> SearchAsync(Guid projectId, Guid userId, string query, int page, int pageSize, CancellationToken cancellationToken = default) =>
        _queryService.SearchAsync(projectId, userId, query, page, pageSize, cancellationToken);

    public Task<List<Card>> FilterAsync(Guid boardId, Guid userId, FilterCriteria filters, CancellationToken cancellationToken = default) =>
        _queryService.FilterAsync(boardId, userId, filters, cancellationToken);

    public async Task<Card> CreateAsync(Guid columnId, Guid userId, CreateCardDto data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(data.Title))
        {
            throw new BadRequestException("Card title is required.");
        }

        var column = await _dbContext.Columns
            .AsNoTracking()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId, cancellationToken);

        if (column is null)
        {
            throw new NotFoundException("Column not found.");
        }
        await _accessGuard.RequireAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);

        var maxPosition = await _dbContext.Cards
            .Where(x => x.ColumnId == columnId)
            .Select(x => (int?)x.Position)
            .MaxAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = columnId,
            Title = data.Title.Trim(),
            Description = NormalizeDescription(data.Description),
            Position = (maxPosition ?? 0) + PositionGap,
            StartDate = data.StartDate?.ToUniversalTime(),
            DueDate = data.DueDate?.ToUniversalTime(),
            EstimatedHours = data.EstimatedHours,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId
        };

        _dbContext.Cards.Add(card);
        _activityRecorder.RecordCard(card.Id, userId, ActivityAction.Created);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.CardCreated(column.Board.ProjectId, card);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Created,
            $"Task created: {card.Title}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} created task '{card.Title}' in list '{column.Name}'.",
            entityType: EntityType.Card,
            entityId: card.Id,
            createdBy: userId,
            (EntityType.Card, card.Id),
            (EntityType.Column, columnId),
            (EntityType.Board, column.BoardId),
            (EntityType.Project, column.Board.ProjectId));

        return card;
    }

    public Task<Card> GetByIdAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default) =>
        _queryService.GetByIdAsync(cardId, userId, cancellationToken);

    public async Task<Card> UpdateAsync(Guid cardId, Guid userId, UpdateCardDto data, CancellationToken cancellationToken = default)
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

        if (card.Version != data.Version)
        {
            throw new ConflictException("Card has been modified. Please refresh and try again.", "VERSION_CONFLICT");
        }

        var oldTitle = card.Title;
        var oldDescription = card.Description;
        var oldStartDate = card.StartDate;
        var oldDueDate = card.DueDate;
        var oldEstimatedHours = card.EstimatedHours;

        card.Title = data.Title.Trim();
        card.Description = NormalizeDescription(data.Description);
        card.StartDate = data.StartDate?.ToUniversalTime();
        card.DueDate = data.DueDate?.ToUniversalTime();
        card.EstimatedHours = data.EstimatedHours;
        card.Version += 1;
        card.UpdatedAt = DateTime.UtcNow;

        if (oldTitle != card.Title)
        {
            _activityRecorder.RecordCard(cardId, userId, ActivityAction.Changed, "title", oldTitle, card.Title);
        }

        if (oldDescription != card.Description)
        {
            _activityRecorder.RecordCard(cardId, userId, ActivityAction.Changed, "description", oldDescription, card.Description);
        }

        if (oldStartDate != card.StartDate)
        {
            _activityRecorder.RecordCard(cardId, userId, card.StartDate.HasValue ? ActivityAction.Changed : ActivityAction.Removed, "start date",
                oldStartDate?.ToString("yyyy-MM-dd"), card.StartDate?.ToString("yyyy-MM-dd"));
        }

        if (oldDueDate != card.DueDate)
        {
            _activityRecorder.RecordCard(cardId, userId, card.DueDate.HasValue ? ActivityAction.Changed : ActivityAction.Removed, "due date",
                oldDueDate?.ToString("yyyy-MM-dd"), card.DueDate?.ToString("yyyy-MM-dd"));
        }

        if (oldEstimatedHours != card.EstimatedHours)
        {
            _activityRecorder.RecordCard(cardId, userId, card.EstimatedHours.HasValue ? ActivityAction.Changed : ActivityAction.Removed, "estimated hours",
                oldEstimatedHours?.ToString("0.##"), card.EstimatedHours?.ToString("0.##"));
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrent update conflict on card {CardId} (user {UserId}, expected version {ExpectedVersion}).",
                cardId, userId, data.Version);
            throw new ConflictException("Card has been modified. Please refresh and try again.", "VERSION_CONFLICT");
        }

        await _projectBroadcaster.CardUpdated(card.Column.Board.ProjectId, card);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Updated,
            $"Task updated: {card.Title}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} updated task '{card.Title}'.",
            entityType: EntityType.Card,
            entityId: card.Id,
            createdBy: userId,
            (EntityType.Card, card.Id),
            (EntityType.Column, card.ColumnId),
            (EntityType.Board, card.Column.BoardId),
            (EntityType.Project, card.Column.Board.ProjectId));

        return card;
    }

    public async Task<Card> ScheduleAsync(Guid cardId, Guid userId, ScheduleCardDto data, CancellationToken cancellationToken = default)
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

        var oldStartDate = card.StartDate;
        var oldDueDate = card.DueDate;

        card.StartDate = data.StartDate?.ToUniversalTime();
        card.DueDate = data.DueDate?.ToUniversalTime();
        card.UpdatedAt = DateTime.UtcNow;

        if (oldStartDate != card.StartDate)
        {
            _activityRecorder.RecordCard(cardId, userId, card.StartDate.HasValue ? ActivityAction.Changed : ActivityAction.Removed, "start date",
                oldStartDate?.ToString("yyyy-MM-dd"), card.StartDate?.ToString("yyyy-MM-dd"));
        }

        if (oldDueDate != card.DueDate)
        {
            _activityRecorder.RecordCard(cardId, userId, card.DueDate.HasValue ? ActivityAction.Changed : ActivityAction.Removed, "due date",
                oldDueDate?.ToString("yyyy-MM-dd"), card.DueDate?.ToString("yyyy-MM-dd"));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.CardUpdated(card.Column.Board.ProjectId, card);

        return card;
    }

    public async Task<Card> MoveAsync(Guid cardId, Guid userId, MoveCardDto data, CancellationToken cancellationToken = default)
    {
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }

        var targetColumn = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == data.ColumnId, cancellationToken);

        if (targetColumn is null)
        {
            throw new NotFoundException("Column not found.");
        }

        if (card.Column.Board.ProjectId != targetColumn.Board.ProjectId)
        {
            throw new BadRequestException("Card can only be moved within the same project.");
        }

        var sourceColumnId = card.ColumnId;
        var sourceColumnName = card.Column.Name;
        await _accessGuard.RequireAccessAsync(targetColumn.Board.ProjectId, userId, ProjectRole.Member);

        var targetCards = await _dbContext.Cards
            .Where(x => x.ColumnId == targetColumn.Id && x.Id != card.Id)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var insertionIndex = Math.Clamp(data.Position, 0, targetCards.Count);
        var before = insertionIndex > 0 ? targetCards[insertionIndex - 1] : null;
        var after = insertionIndex < targetCards.Count ? targetCards[insertionIndex] : null;

        var now = DateTime.UtcNow;
        var requiresRenumber = false;
        var newPosition = PositionGap;

        if (before is not null && after is not null)
        {
            var gap = after.Position - before.Position;
            requiresRenumber = gap < 2;
            newPosition = (before.Position + after.Position) / 2;
        }
        else if (before is null && after is not null)
        {
            newPosition = after.Position - PositionGap;
            requiresRenumber = targetCards.Any(x => x.Position == newPosition);
        }
        else if (before is not null)
        {
            newPosition = before.Position + PositionGap;
            requiresRenumber = targetCards.Any(x => x.Position == newPosition);
        }

        if (!requiresRenumber)
        {
            card.ColumnId = targetColumn.Id;
            card.Position = newPosition;
            card.UpdatedAt = now;

            if (sourceColumnId != targetColumn.Id)
            {
                _activityRecorder.RecordCard(cardId, userId, ActivityAction.Moved, "list", sourceColumnName, targetColumn.Name);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            await _projectBroadcaster.CardMoved(targetColumn.Board.ProjectId, card);

            var fastPathActor = await _notificationFanout.GetActorLabelAsync(userId);
            await _notificationFanout.FanOutAsync(
                userId,
                NotificationType.Moved,
                $"Task moved: {card.Title}",
                $"{fastPathActor} moved task '{card.Title}' from '{sourceColumnName}' to '{targetColumn.Name}'.",
                entityType: EntityType.Card,
                entityId: card.Id,
                createdBy: userId,
                (EntityType.Card, card.Id),
                (EntityType.Column, sourceColumnId),
                (EntityType.Column, targetColumn.Id),
                (EntityType.Board, targetColumn.BoardId),
                (EntityType.Project, targetColumn.Board.ProjectId));

            return card;
        }

        _logger.LogInformation(
            "Renumbering positions in column {ColumnId} for card {CardId} (user {UserId}); {CardCount} cards affected.",
            targetColumn.Id, cardId, userId, targetCards.Count + 1);

        var reordered = BuildOrderedCards(targetCards, card, insertionIndex);

        for (var index = 0; index < reordered.Count; index++)
        {
            reordered[index].ColumnId = targetColumn.Id;
            reordered[index].Position = (index + 1) * PositionGap;
            reordered[index].UpdatedAt = now;
        }

        if (sourceColumnId != targetColumn.Id)
        {
            _activityRecorder.RecordCard(cardId, userId, ActivityAction.Moved, "list", sourceColumnName, targetColumn.Name);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.CardMoved(targetColumn.Board.ProjectId, card);

        var actor = await _notificationFanout.GetActorLabelAsync(userId);
        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Moved,
            $"Task moved: {card.Title}",
            $"{actor} moved task '{card.Title}' from '{sourceColumnName}' to '{targetColumn.Name}'.",
            entityType: EntityType.Card,
            entityId: card.Id,
            createdBy: userId,
            (EntityType.Card, card.Id),
            (EntityType.Column, sourceColumnId),
            (EntityType.Column, targetColumn.Id),
            (EntityType.Board, targetColumn.BoardId),
            (EntityType.Project, targetColumn.Board.ProjectId));

        return card;
    }

    public Task AssignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default) =>
        _assignmentService.AssignTagAsync(cardId, tagId, userId, cancellationToken);

    public Task UnassignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default) =>
        _assignmentService.UnassignTagAsync(cardId, tagId, userId, cancellationToken);

    public Task AssignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default) =>
        _assignmentService.AssignUserAsync(cardId, assigneeUserId, userId, cancellationToken);

    public Task UnassignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default) =>
        _assignmentService.UnassignUserAsync(cardId, assigneeUserId, userId, cancellationToken);

    public Task<Subtask> CreateSubtaskAsync(Guid cardId, Guid userId, CreateSubtaskDto data, CancellationToken cancellationToken = default) =>
        _subtaskService.CreateAsync(cardId, userId, data, cancellationToken);

    public Task<Subtask> UpdateSubtaskAsync(Guid subtaskId, Guid userId, UpdateSubtaskDto data, CancellationToken cancellationToken = default) =>
        _subtaskService.UpdateAsync(subtaskId, userId, data, cancellationToken);

    public Task DeleteSubtaskAsync(Guid subtaskId, Guid userId, CancellationToken cancellationToken = default) =>
        _subtaskService.DeleteAsync(subtaskId, userId, cancellationToken);

    public Task<SubtaskCountsDto> GetSubtaskCountsAsync(Guid cardId, Guid userId) =>
        _subtaskService.GetCountsAsync(cardId, userId);

    public Task ArchiveAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.ArchiveAsync(cardId, userId, cancellationToken);

    public Task RestoreAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.RestoreAsync(cardId, userId, cancellationToken);

    public Task PurgeAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.PurgeAsync(cardId, userId, cancellationToken);

    public Task<PaginatedResponse<Card>> ListArchivedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        _queryService.ListArchivedAsync(userId, page, pageSize, cancellationToken);

    public Task<PaginatedResponse<Card>> ListArchivedByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        _queryService.ListArchivedByBoardAsync(boardId, userId, page, pageSize, cancellationToken);

    private static string? NormalizeDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        var trimmed = description.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static List<Card> BuildOrderedCards(IReadOnlyList<Card> targetCards, Card movable, int insertionIndex)
    {
        var ordered = targetCards.ToList();
        var clampedIndex = Math.Clamp(insertionIndex, 0, ordered.Count);
        ordered.Insert(clampedIndex, movable);
        return ordered;
    }
}
