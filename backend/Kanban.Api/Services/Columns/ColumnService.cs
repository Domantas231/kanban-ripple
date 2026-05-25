using System.Data;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kanban.Api.Services.Columns;

public sealed class ColumnService : IColumnService
{
    private const int PositionGap = Positioning.Gap;

    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly INotificationFanout _notificationFanout;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IColumnArchiveService _archiveService;
    private readonly ILogger<ColumnService> _logger;

    public ColumnService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder,
        IColumnArchiveService archiveService,
        INotificationFanout notificationFanout,
        IProjectBroadcaster projectBroadcaster,
        ILogger<ColumnService> logger)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
        _archiveService = archiveService;
        _notificationFanout = notificationFanout;
        _projectBroadcaster = projectBroadcaster;
        _logger = logger;
    }

    public async Task<Column> CreateAsync(Guid boardId, Guid userId, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Column name is required.");
        }

        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member);

        var maxPosition = await _dbContext.Columns
            .Where(x => x.BoardId == boardId)
            .Select(x => (int?)x.Position)
            .MaxAsync(cancellationToken);

        var nextPosition = (maxPosition ?? 0) + PositionGap;
        var now = DateTime.UtcNow;

        var column = new Column
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = name.Trim(),
            Position = nextPosition,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Columns.Add(column);
        _activityRecorder.RecordColumn(column.Id, userId, ActivityAction.Created);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.ColumnCreated(board.ProjectId, column);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Created,
            $"List created: {column.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} created list '{column.Name}'.",
            entityType: EntityType.Column,
            entityId: column.Id,
            createdBy: userId,
            (EntityType.Column, column.Id),
            (EntityType.Board, boardId),
            (EntityType.Project, board.ProjectId));

        return column;
    }

    public async Task<Column> GetByIdAsync(Guid columnId, Guid userId)
    {
        var column = await _dbContext.Columns
            .AsNoTracking()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
        {
            throw new NotFoundException("Column not found.");
        }
        await _accessGuard.RequireAccessAsync(column.Board.ProjectId, userId, ProjectRole.Viewer);

        return column;
    }

    public async Task<IReadOnlyList<Column>> ListAsync(Guid boardId, Guid userId)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Viewer);

        return await _dbContext.Columns
            .AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Column> UpdateAsync(Guid columnId, Guid userId, UpdateColumnDto data, CancellationToken cancellationToken = default)
    {
        var column = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId, cancellationToken);

        if (column is null)
        {
            throw new NotFoundException("Column not found.");
        }
        await _accessGuard.RequireAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);

        var oldName = column.Name;
        column.Name = data.Name.Trim();
        column.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(oldName, column.Name, StringComparison.Ordinal))
        {
            _activityRecorder.RecordColumn(column.Id, userId, ActivityAction.Changed, "name", oldName, column.Name);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.ColumnUpdated(column.Board.ProjectId, column);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Updated,
            $"List updated: {column.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} updated list '{column.Name}'.",
            entityType: EntityType.Column,
            entityId: column.Id,
            createdBy: userId,
            (EntityType.Column, column.Id),
            (EntityType.Board, column.BoardId),
            (EntityType.Project, column.Board.ProjectId));

        return column;
    }

    public async Task<Column> ReorderAsync(Guid columnId, Guid userId, ReorderColumnDto data, CancellationToken cancellationToken = default)
    {
        if (data.BeforeColumnId == columnId || data.AfterColumnId == columnId)
        {
            throw new BadRequestException("A column cannot be used as its own anchor.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var column = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId, cancellationToken);

        if (column is null)
        {
            throw new NotFoundException("Column not found.");
        }
        await _accessGuard.RequireAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);

        var columns = await _dbContext.Columns
            .Where(x => x.BoardId == column.BoardId)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (columns.Count <= 1)
        {
            column.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return column;
        }

        var movable = columns.First(x => x.Id == columnId);
        var otherColumns = columns.Where(x => x.Id != columnId).ToList();

        Column? before = null;
        Column? after = null;

        if (data.BeforeColumnId is not null)
        {
            before = otherColumns.FirstOrDefault(x => x.Id == data.BeforeColumnId.Value)
                ?? throw new BadRequestException("Before anchor column not found in board.");
        }

        if (data.AfterColumnId is not null)
        {
            after = otherColumns.FirstOrDefault(x => x.Id == data.AfterColumnId.Value)
                ?? throw new BadRequestException("After anchor column not found in board.");
        }

        if (before is not null && after is not null)
        {
            var beforeIndex = otherColumns.FindIndex(x => x.Id == before.Id);
            var afterIndex = otherColumns.FindIndex(x => x.Id == after.Id);
            if (beforeIndex >= afterIndex)
            {
                throw new BadRequestException("Before anchor must appear before after anchor.");
            }
        }

        var now = DateTime.UtcNow;
        var requiresRenumber = false;
        int newPosition;

        if (before is not null && after is not null)
        {
            var gap = after.Position - before.Position;
            requiresRenumber = gap < 2;
            newPosition = (before.Position + after.Position) / 2;
        }
        else if (before is null && after is not null)
        {
            newPosition = after.Position - PositionGap;
            requiresRenumber = otherColumns.Any(x => x.Position == newPosition);
        }
        else
        {
            newPosition = before!.Position + PositionGap;
            requiresRenumber = otherColumns.Any(x => x.Position == newPosition);
        }

        if (!requiresRenumber)
        {
            movable.Position = newPosition;
            movable.UpdatedAt = now;
            _activityRecorder.RecordColumn(movable.Id, userId, ActivityAction.Moved);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            await _projectBroadcaster.ColumnUpdated(column.Board.ProjectId, movable);

            await _notificationFanout.FanOutAsync(
                userId,
                NotificationType.Moved,
                $"List moved: {movable.Name}",
                $"{await _notificationFanout.GetActorLabelAsync(userId)} moved list '{movable.Name}'.",
                entityType: EntityType.Column,
                entityId: movable.Id,
                createdBy: userId,
                (EntityType.Column, movable.Id),
                (EntityType.Board, movable.BoardId),
                (EntityType.Project, column.Board.ProjectId));

            return movable;
        }

        _logger.LogInformation(
            "Renumbering positions in board {BoardId} for column {ColumnId} (user {UserId}); {ColumnCount} columns affected.",
            column.BoardId, columnId, userId, otherColumns.Count + 1);

        var reordered = BuildOrderedColumns(otherColumns, movable, before?.Id, after?.Id);

        for (var index = 0; index < reordered.Count; index++)
        {
            reordered[index].Position = (index + 1) * PositionGap;
            reordered[index].UpdatedAt = now;
        }

        _activityRecorder.RecordColumn(movable.Id, userId, ActivityAction.Moved);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await _projectBroadcaster.ColumnUpdated(column.Board.ProjectId, movable);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Moved,
            $"List moved: {movable.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} moved list '{movable.Name}'.",
            entityType: EntityType.Column,
            entityId: movable.Id,
            createdBy: userId,
            (EntityType.Column, movable.Id),
            (EntityType.Board, movable.BoardId),
            (EntityType.Project, column.Board.ProjectId));

        return movable;
    }

    public Task ArchiveAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.ArchiveAsync(columnId, userId, cancellationToken);

    public Task RestoreAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.RestoreAsync(columnId, userId, cancellationToken);

    public Task PurgeAsync(Guid columnId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.PurgeAsync(columnId, userId, cancellationToken);

    public async Task<IReadOnlyList<Column>> ListArchivedByBoardAsync(Guid boardId, Guid userId)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member);

        return await _dbContext.Columns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.BoardId == boardId && x.DeletedAt != null)
            .OrderByDescending(x => x.DeletedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    private static List<Column> BuildOrderedColumns(
        IReadOnlyList<Column> otherColumns,
        Column moving,
        Guid? beforeId,
        Guid? afterId)
    {
        var ordered = new List<Column>(otherColumns);

        if (beforeId is null && afterId is not null)
        {
            var insertBeforeIndex = ordered.FindIndex(x => x.Id == afterId.Value);
            ordered.Insert(insertBeforeIndex, moving);
            return ordered;
        }

        if (beforeId is not null && afterId is null)
        {
            var insertAfterIndex = ordered.FindIndex(x => x.Id == beforeId.Value);
            ordered.Insert(insertAfterIndex + 1, moving);
            return ordered;
        }

        var afterAnchorIndex = ordered.FindIndex(x => x.Id == afterId!.Value);
        ordered.Insert(afterAnchorIndex, moving);
        return ordered;
    }

}
